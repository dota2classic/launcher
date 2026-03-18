using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Integration;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Util;
using Microsoft.Win32;

namespace d2c_launcher.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISteamManager _steamManager;
    private readonly ISettingsStorage _settingsStorage;
    private readonly IGameLaunchSettingsStorage _launchSettingsStorage;
    private readonly ISteamAuthApi _steamAuthApi;
    private readonly IBackendApiService _backendApiService;
    private readonly IChatViewModelFactory _chatViewModelFactory;
    private readonly IQueueSocketService _queueSocketService;
    private readonly ICvarSettingsProvider _cvarProvider;
    private readonly IVideoSettingsProvider _videoProvider;
    private readonly UpdateService _updateService;
    private readonly ILocalManifestService _localManifestService;
    private readonly IManifestDiffService _manifestDiffService;
    private readonly IGameDownloadService _gameDownloadService;
    private readonly RedistInstallService _redistInstallService;
    private readonly IContentRegistryService _registryService;
    private readonly IWindowService _windowService;
    private readonly IUiDispatcher _uiDispatcher;

    /// <summary>Pending spectate match ID received before the Launcher state was entered.</summary>
    private int? _pendingSpectateMatchId;

    /// <summary>Cleanup action to run when the current content VM is disposed.</summary>
    private Action? _currentVmCleanup;

    /// <summary>Error message to display on the SelectGame screen after an invalid directory reset.</summary>
    private string? _pendingSelectGameError;

    [ObservableProperty]
    private AppState _appState;

    [ObservableProperty]
    private object? _currentContentViewModel;

    [ObservableProperty]
    private bool _updateAvailable;

    [ObservableProperty]
    private string? _releaseNotes;

    public bool IsSteamRunning => _steamManager.SteamStatus is SteamStatus.Running or SteamStatus.Offline;

    public string WindowTitle { get; } = BuildWindowTitle();

    private static string BuildWindowTitle()
    {
        var asm = Assembly.GetExecutingAssembly();
        var version = (asm.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
                          ?.InformationalVersion
                          ?.Split('+')[0])
                      ?? asm.GetName().Version?.ToString(3)
                      ?? "?";
        return $"dotaclassic v{version}";
    }

    public MainWindowViewModel(
        ISteamManager steamManager,
        ISettingsStorage settingsStorage,
        IGameLaunchSettingsStorage launchSettingsStorage,
        ISteamAuthApi steamAuthApi,
        IBackendApiService backendApiService,
        IQueueSocketService queueSocketService,
        IChatViewModelFactory chatViewModelFactory,
        ICvarSettingsProvider cvarProvider,
        IVideoSettingsProvider videoProvider,
        UpdateService updateService,
        ILocalManifestService localManifestService,
        IManifestDiffService manifestDiffService,
        IGameDownloadService gameDownloadService,
        RedistInstallService redistInstallService,
        IContentRegistryService registryService,
        IWindowService windowService,
        IUiDispatcher uiDispatcher)
    {
        _steamManager = steamManager;
        _settingsStorage = settingsStorage;
        _launchSettingsStorage = launchSettingsStorage;
        _steamAuthApi = steamAuthApi;
        _backendApiService = backendApiService;
        _queueSocketService = queueSocketService;
        _chatViewModelFactory = chatViewModelFactory;
        _cvarProvider = cvarProvider;
        _videoProvider = videoProvider;
        _updateService = updateService;
        _localManifestService = localManifestService;
        _manifestDiffService = manifestDiffService;
        _gameDownloadService = gameDownloadService;
        _redistInstallService = redistInstallService;
        _registryService = registryService;
        _windowService = windowService;
        _uiDispatcher = uiDispatcher;

        // Eagerly prefetch the registry so it's cached by the time GameDownloadViewModel needs it.
        var initialGameDir = _settingsStorage.Get().GameDirectory;
        if (!string.IsNullOrWhiteSpace(initialGameDir) && Directory.Exists(initialGameDir))
            _ = _registryService.GetAsync();

        // Compute and enter initial state
        var initialState = AppStateMachine.OnSteamUpdate(
            AppState.CheckingSteam,
            steamManager.SteamStatus,
            steamManager.CurrentUser != null,
            HasValidGameDir());
        EnterState(initialState);

        _steamManager.OnSteamStatusUpdated += _ =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                OnPropertyChanged(nameof(IsSteamRunning));
                TransitionOnSteamUpdate();
            });
        };

        _steamManager.OnUserUpdated += _ =>
        {
            Dispatcher.UIThread.Post(TransitionOnSteamUpdate);
        };

        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        var (hasUpdate, notes) = await _updateService.CheckAndDownloadAsync();
        if (hasUpdate)
            Dispatcher.UIThread.Post(() =>
            {
                ReleaseNotes = notes;
                UpdateAvailable = true;
            });
    }

    [RelayCommand]
    private void ApplyUpdate()
    {
        // Release the single-instance mutex before Velopack launches the new process,
        // otherwise the new instance sees the mutex held and exits immediately.
        App.SingleInstance?.Dispose();
        App.SingleInstance = null;
        _updateService.ApplyAndRestart();
    }

    // ── State transitions ────────────────────────────────────────────────────

    /// <summary>Fired by SteamManager events — recomputes state from current Steam conditions.</summary>
    private void TransitionOnSteamUpdate()
    {
        var next = AppStateMachine.OnSteamUpdate(
            AppState,
            _steamManager.SteamStatus,
            _steamManager.CurrentUser != null,
            HasValidGameDir());
        EnterState(next);
    }

    /// <summary>
    /// Enters a new <see cref="AppState"/>, disposing the previous content VM and
    /// creating the appropriate new one. No-ops if the state hasn't changed.
    /// </summary>
    private void EnterState(AppState newState)
    {
        if (AppState == newState && CurrentContentViewModel != null)
            return;

        AppState = newState;

        switch (newState)
        {
            case AppState.CheckingSteam:
                DisposeCurrentVm();
                CurrentContentViewModel = new LoadingViewModel();
                break;

            case AppState.SteamNotRunning:
            case AppState.SteamOffline:
                DisposeCurrentVm();
                var steamFirstVm = new LaunchSteamFirstViewModel
                {
                    TryAgainCallback = () =>
                    {
                        _steamManager.ResetBridgeFailStreak();
                        TransitionOnSteamUpdate();
                    },
                    GetDiagnostics = CollectSteamDiagnostics
                };
                _steamManager.OnSteamPolled += steamFirstVm.FireCheck;
                _currentVmCleanup = () => _steamManager.OnSteamPolled -= steamFirstVm.FireCheck;
                CurrentContentViewModel = steamFirstVm;
                break;

            case AppState.SelectGameDirectory:
                // Guard: already showing — don't recreate
                if (CurrentContentViewModel is SelectGameViewModel)
                    return;
                DisposeCurrentVm();
                var settings = _settingsStorage.Get();
                var selectVm = new SelectGameViewModel(_registryService)
                {
                    DownloadPathError = _pendingSelectGameError,
                    ExistingDlcIds = settings.SelectedDlcIds,
                    OnDlcSelectionSaved = ids =>
                    {
                        var s = _settingsStorage.Get();
                        s.SelectedDlcIds = ids;
                        _settingsStorage.Save(s);
                    },
                    GameDirectorySelected = path =>
                    {
                        var s = _settingsStorage.Get();
                        s.GameDirectory = path;
                        _settingsStorage.Save(s);
                        Dispatcher.UIThread.Post(() => EnterState(AppStateMachine.OnGameDirSelected(AppState)));
                    }
                };
                _pendingSelectGameError = null;
                CurrentContentViewModel = selectVm;
                break;

            case AppState.VerifyingGame:
                // Guard: already verifying — don't restart
                if (CurrentContentViewModel is GameDownloadViewModel)
                    return;
                EnterVerifyingGame();
                break;

            case AppState.Launcher:
                // Guard: already on main screen — don't recreate
                if (CurrentContentViewModel is MainLauncherViewModel)
                    return;
                EnterLauncher();
                break;
        }
    }

    private void EnterVerifyingGame(List<string>? packageIdsToRemove = null)
    {
        var gameDir = ResolveGameDir();
        if (gameDir == null)
        {
            // Directory was deleted between state computation and here — recheck without a game dir
            EnterState(AppStateMachine.OnSteamUpdate(AppState, _steamManager.SteamStatus, _steamManager.CurrentUser != null, false));
            return;
        }

        DisposeCurrentVm();

        var settings = _settingsStorage.Get();
        bool needDefenderModal = settings.ShouldShowDefenderPrompt;

        var vm = new GameDownloadViewModel(_registryService, _localManifestService, _manifestDiffService, _gameDownloadService, _redistInstallService)
        {
            GameDirectory = gameDir,
            SelectedDlcIds = settings.SelectedDlcIds ?? [],
            NeedDefenderModal = needDefenderModal,
            PackageIdsToRemove = packageIdsToRemove,
            OnDefenderDecisionMade = accepted =>
            {
                var s = _settingsStorage.Get();
                if (accepted) s.DefenderExclusionPath = gameDir;
                s.DefenderPromptAnswered = true;
                _settingsStorage.Save(s);
            },
            OnPackagesInstalled = ids =>
            {
                var s = _settingsStorage.Get();
                s.InstalledPackageIds = ids;
                _settingsStorage.Save(s);
            },
            OnCompleted = () => Dispatcher.UIThread.Post(() => EnterState(AppStateMachine.OnVerificationCompleted(AppState))),
            OnInvalidGameDirectory = errorMessage => Dispatcher.UIThread.Post(() =>
            {
                var s = _settingsStorage.Get();
                s.GameDirectory = null;
                _settingsStorage.Save(s);
                // Reset AppState before entering — otherwise the sticky rule in OnSteamUpdate
                // keeps us in VerifyingGame even with no valid game directory.
                AppState = AppState.CheckingSteam;
                _pendingSelectGameError = errorMessage;
                EnterState(AppState.SelectGameDirectory);
            }),
        };
        CurrentContentViewModel = vm;
        vm.StartAsync();
    }

    private void EnterLauncher()
    {
        DisposeCurrentVm();
        var vm = new MainLauncherViewModel(
            _steamManager, _settingsStorage, _launchSettingsStorage, _cvarProvider, _videoProvider,
            _backendApiService, _queueSocketService, _registryService, _chatViewModelFactory, _windowService,
            _steamAuthApi, _uiDispatcher);
        vm.OnGameDirectoryChanged = _ => Dispatcher.UIThread.Post(() => EnterState(AppStateMachine.OnGameDirChanged(AppState)));
        vm.RequestGameDirectoryChange = () => Dispatcher.UIThread.Post(() => EnterState(AppState.SelectGameDirectory));
        vm.OnDlcChanged = removedIds => Dispatcher.UIThread.Post(() =>
        {
            AppState = AppState.VerifyingGame;
            EnterVerifyingGame(removedIds);
        });
        vm.RequestReverify = () => Dispatcher.UIThread.Post(() =>
        {
            AppState = AppState.VerifyingGame;
            EnterVerifyingGame();
        });
        CurrentContentViewModel = vm;

        if (_pendingSpectateMatchId.HasValue)
        {
            var matchId = _pendingSpectateMatchId.Value;
            _pendingSpectateMatchId = null;
            vm.Launch.SpectateMatch(matchId);
        }
    }

    /// <summary>
    /// Handles a d2c:// protocol URL (e.g. "d2c://spectate/123").
    /// Safe to call from any thread.
    /// </summary>
    public void HandleProtocolUrl(string url)
    {
        AppLog.Info($"[Protocol] handling URL: {url}");
        if (TryParseSpectateUrl(url, out var matchId))
            Dispatcher.UIThread.Post(() => HandleSpectate(matchId));
        else
            AppLog.Info($"[Protocol] unrecognised URL: {url}");
    }

    private static bool TryParseSpectateUrl(string url, out int matchId)
    {
        matchId = 0;
        // Expected: d2c://spectate/12345  (scheme is stripped to "spectate/12345" by Uri or left as-is)
        var lower = url.ToLowerInvariant().TrimEnd('/');
        const string prefix = "d2c://spectate/";
        if (!lower.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        return int.TryParse(url[prefix.Length..], out matchId);
    }

    private void HandleSpectate(int matchId)
    {
        if (CurrentContentViewModel is MainLauncherViewModel launcherVm)
            launcherVm.Launch.SpectateMatch(matchId);
        else
            _pendingSpectateMatchId = matchId;
    }

    private Dictionary<string, string> CollectSteamDiagnostics()
    {
        var attrs = new Dictionary<string, string>();

        // Steam process
        var steamProcs = Process.GetProcessesByName("steam");
        attrs["steam_process_count"] = steamProcs.Length.ToString();
        foreach (var p in steamProcs) p.Dispose();

        // Registry: ActiveUser
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess", false);
            var raw = key?.GetValue("ActiveUser");
            attrs["active_user_registry"] = raw?.ToString() ?? "null";
        }
        catch (Exception ex)
        {
            attrs["active_user_registry"] = $"error:{ex.GetType().Name}";
        }

        // Registry: Steam install path
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam", false);
            var path = key?.GetValue("SteamPath")?.ToString();
            attrs["steam_path_registry"] = path ?? "null";
        }
        catch (Exception ex)
        {
            attrs["steam_path_registry"] = $"error:{ex.GetType().Name}";
        }

        // Bridge exe
        var bridgePath = Path.Combine(AppContext.BaseDirectory, "d2c-steam-bridge.exe");
        attrs["bridge_exe_exists"] = File.Exists(bridgePath).ToString();

        // SteamManager state
        attrs["steam_status"] = _steamManager.SteamStatus.ToString();
        attrs["bridge_fail_streak"] = _steamManager.BridgeFailStreak.ToString();
        attrs["bridge_last_status"] = _steamManager.LastBridgeStatus ?? "null";

        // Elevation (running as admin breaks HKCU reads when Steam runs non-elevated)
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            attrs["is_elevated"] = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator).ToString();
        }
        catch
        {
            attrs["is_elevated"] = "unknown";
        }

        return attrs;
    }

    private void DisposeCurrentVm()
    {
        _currentVmCleanup?.Invoke();
        _currentVmCleanup = null;
        (CurrentContentViewModel as System.IDisposable)?.Dispose();
        CurrentContentViewModel = null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Returns true if the settings game directory exists on disk.</summary>
    private bool HasValidGameDir()
    {
        var dir = _settingsStorage.Get().GameDirectory;
        return !string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir);
    }

    /// <summary>
    /// Returns the configured game directory, or null if it's missing/stale.
    /// Clears a stale path from settings so it won't be reused.
    /// </summary>
    private string? ResolveGameDir()
    {
        var settings = _settingsStorage.Get();
        var dir = settings.GameDirectory;
        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            return dir;

        if (!string.IsNullOrWhiteSpace(dir))
        {
            settings.GameDirectory = null;
            _settingsStorage.Save(settings);
        }

        return null;
    }
}
