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

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan BackgroundVerificationDelay = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan RemoteUpdatePollInterval = TimeSpan.FromMinutes(3);

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
    private readonly IRemoteManifestService _remoteManifestService;
    private readonly RedistInstallService _redistInstallService;
    private readonly IContentRegistryService _registryService;
    private readonly IWindowService _windowService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly ITriviaRepository _triviaRepository;
    private readonly ITimerFactory _timerFactory;
    private readonly INetConService _netConService;
    private readonly IGameWindowService _gameWindowService;
    private readonly IDotakeysProfileService _dotakeysProfileService;
    private readonly IToastNotificationService _toastNotificationService;
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly IPaidActionService _paidActions;
    private readonly AppStartupContext _startupContext;
    private readonly IUiTimer _remoteUpdatePollTimer;

    /// <summary>Pending spectate match ID received before the Launcher state was entered.</summary>
    private int? _pendingSpectateMatchId;

    /// <summary>Cleanup action to run when the current content VM is disposed.</summary>
    private Action? _currentVmCleanup;

    /// <summary>Error message to display on the SelectGame screen after an invalid directory reset.</summary>
    private string? _pendingSelectGameError;

    private bool _backgroundWindowMode;
    private bool _verificationSatisfied;
    private bool _backgroundVerificationStarted;
    private int _remoteUpdateCheckInFlight; // 0 = idle, 1 = in-flight; use Interlocked
    private GameManifest? _verifiedLocalManifestSnapshot;

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
        IRemoteManifestService remoteManifestService,
        RedistInstallService redistInstallService,
        IContentRegistryService registryService,
        IWindowService windowService,
        IUiDispatcher uiDispatcher,
        ITriviaRepository triviaRepository,
        ITimerFactory timerFactory,
        INetConService netConService,
        IGameWindowService gameWindowService,
        IDotakeysProfileService dotakeysProfileService,
        IToastNotificationService toastNotificationService,
        IStartupRegistrationService startupRegistrationService,
        IPaidActionService paidActions,
        AppStartupContext startupContext)
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
        _remoteManifestService = remoteManifestService;
        _redistInstallService = redistInstallService;
        _registryService = registryService;
        _windowService = windowService;
        _uiDispatcher = uiDispatcher;
        _triviaRepository = triviaRepository;
        _timerFactory = timerFactory;
        _netConService = netConService;
        _gameWindowService = gameWindowService;
        _dotakeysProfileService = dotakeysProfileService;
        _toastNotificationService = toastNotificationService;
        _startupRegistrationService = startupRegistrationService;
        _paidActions = paidActions;
        _startupContext = startupContext;
        _backgroundWindowMode = startupContext.IsBackgroundStart;
        _remoteUpdatePollTimer = timerFactory.Create();
        _remoteUpdatePollTimer.Interval = RemoteUpdatePollInterval;
        _remoteUpdatePollTimer.Tick += OnRemoteUpdatePollTick;

        _windowService.WindowShown += OnWindowShown;

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
        if (ShouldRouteToBackgroundLauncher(initialState))
            initialState = AppState.Launcher;
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
        if (ShouldRouteToBackgroundLauncher(next))
            next = AppState.Launcher;
        EnterState(next);
    }

    private bool ShouldRouteToBackgroundLauncher(AppState nextState)
    {
        return _backgroundWindowMode
            && nextState == AppState.VerifyingGame
            && !_verificationSatisfied;
    }

    private void OnWindowShown()
    {
        _backgroundWindowMode = false;
        if (_startupContext.IsBackgroundStart &&
            !_verificationSatisfied &&
            CurrentContentViewModel is MainLauncherViewModel &&
            HasValidGameDir())
        {
            EnterForegroundVerification();
        }
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
                StopRemoteUpdatePolling();
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
                StopRemoteUpdatePolling();
                break;

            case AppState.VerifyingGame:
                // Guard: already verifying — don't restart
                if (CurrentContentViewModel is GameDownloadViewModel)
                    return;
                EnterVerifyingGame(VerificationMode.Foreground);
                StopRemoteUpdatePolling();
                break;

            case AppState.Launcher:
                // Guard: already on main screen — don't recreate
                if (CurrentContentViewModel is MainLauncherViewModel)
                    return;
                EnterLauncher();
                break;
        }
    }

    private void EnterVerifyingGame(VerificationMode verificationMode)
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

        var vm = new GameDownloadViewModel(_localManifestService, _manifestDiffService, _gameDownloadService, _redistInstallService, _remoteManifestService)
        {
            GameDirectory = gameDir,
            VerificationMode = verificationMode,
            SelectedDlcIds = settings.SelectedDlcIds ?? [],
            NeedDefenderModal = needDefenderModal,

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
            OnCompletedWithManifest = manifest => _verifiedLocalManifestSnapshot = manifest,
            OnCompleted = () => Dispatcher.UIThread.Post(() =>
            {
                _verificationSatisfied = true;
                EnterState(AppStateMachine.OnVerificationCompleted(AppState));
            }),
            OnInvalidGameDirectory = errorMessage => Dispatcher.UIThread.Post(() =>
            {
                _verifiedLocalManifestSnapshot = null;
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

    private void EnterForegroundVerification()
    {
        if (CurrentContentViewModel is GameDownloadViewModel)
            return;

        AppState = AppState.VerifyingGame;
        EnterVerifyingGame(VerificationMode.Foreground);
    }

    private void EnterLauncher()
    {
        DisposeCurrentVm();
        var vm = new MainLauncherViewModel(
            _steamManager, _settingsStorage, _launchSettingsStorage, _cvarProvider, _videoProvider,
            _backendApiService, _queueSocketService, _registryService, _chatViewModelFactory, _windowService,
            _steamAuthApi, _uiDispatcher, _triviaRepository, _timerFactory, _netConService, _gameWindowService,
            _dotakeysProfileService, _toastNotificationService, _startupRegistrationService, _paidActions);
        vm.OnGameDirectoryChanged = _ => Dispatcher.UIThread.Post(() => EnterState(AppStateMachine.OnGameDirChanged(AppState)));
        vm.RequestGameDirectoryChange = () => Dispatcher.UIThread.Post(() => EnterState(AppState.SelectGameDirectory));
        void StartForegroundVerification() => Dispatcher.UIThread.Post(() =>
        {
            AppState = AppState.VerifyingGame;
            EnterVerifyingGame(VerificationMode.Foreground);
        });
        vm.OnDlcChanged = StartForegroundVerification;
        vm.RequestReverify = StartForegroundVerification;
        vm.RequestInstallGameUpdate = StartForegroundVerification;
        vm.SetGameUpdatePending(false);
        CurrentContentViewModel = vm;
        if (_startupContext.IsBackgroundStart && !_verificationSatisfied)
            StartBackgroundVerificationIfNeeded();
        StartRemoteUpdatePollingIfReady();

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
        if (ShouldVerifyBeforeProtocolAction(url))
        {
            if (TryParsePartyInviteResponseUrl(url, out var pendingInviteId, out var pendingAcceptInvite))
                _queueSocketService.AcceptPartyInviteAsync(pendingInviteId, pendingAcceptInvite)
                    .FireAndForget($"HandlePartyInviteResponse ({(pendingAcceptInvite ? "accept" : "decline")}) before verification");
            else if (TryParseReadyCheckResponseUrl(url, out var pendingRoomId, out var pendingAcceptReadyCheck))
                _queueSocketService.SetReadyCheckAsync(pendingRoomId, pendingAcceptReadyCheck)
                    .FireAndForget($"HandleReadyCheckResponse ({(pendingAcceptReadyCheck ? "accept" : "decline")}) before verification");

            Dispatcher.UIThread.Post(() =>
            {
                if (TryParseSpectateUrl(url, out var pendingSpectateMatchId))
                    _pendingSpectateMatchId = pendingSpectateMatchId;

                EnterForegroundVerification();
            });
            return;
        }

        if (TryParseSpectateUrl(url, out var matchId))
            Dispatcher.UIThread.Post(() => HandleSpectate(matchId));
        else if (url.Equals("d2c://game", StringComparison.OrdinalIgnoreCase))
            Dispatcher.UIThread.Post(HandleNavigateToGame);
        else if (TryParseEnterQueueUrl(url, out var modeId))
            Dispatcher.UIThread.Post(() => HandleEnterQueue(modeId));
        else if (TryParsePartyInviteResponseUrl(url, out var inviteId, out var acceptInvite))
            Dispatcher.UIThread.Post(() => HandlePartyInviteResponse(inviteId, acceptInvite));
        else if (TryParseReadyCheckResponseUrl(url, out var roomId, out var acceptReadyCheck))
            Dispatcher.UIThread.Post(() => HandleReadyCheckResponse(roomId, acceptReadyCheck));
        else
            AppLog.Info($"[Protocol] unrecognised URL: {url}");
    }

    private bool ShouldVerifyBeforeProtocolAction(string url)
    {
        return _startupContext.IsBackgroundStart
            && !_verificationSatisfied
            && CurrentContentViewModel is MainLauncherViewModel
            && HasValidGameDir()
            && !url.EndsWith("/decline", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseEnterQueueUrl(string url, out int modeId)
    {
        modeId = 0;
        const string prefix = "d2c://enter-queue/";
        if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        return int.TryParse(url[prefix.Length..], out modeId);
    }

    private void HandleNavigateToGame()
    {
        if (CurrentContentViewModel is MainLauncherViewModel vm)
            vm.NavigateTo(LauncherTab.Play);
    }

    private void HandleEnterQueue(int modeId)
    {
        if (CurrentContentViewModel is MainLauncherViewModel vm)
        {
            vm.NavigateTo(LauncherTab.Play);
            vm.Queue.EnterQueueForModeAsync(modeId).FireAndForget("HandleEnterQueue (toast button)");
        }
    }

    private static bool TryParsePartyInviteResponseUrl(string url, out string inviteId, out bool accept)
    {
        inviteId = "";
        accept = false;
        return TryParseBooleanActionUrl(url, "d2c://party-invite/", out inviteId, out accept);
    }

    private static bool TryParseReadyCheckResponseUrl(string url, out string roomId, out bool accept)
    {
        roomId = "";
        accept = false;
        return TryParseBooleanActionUrl(url, "d2c://ready-check/", out roomId, out accept);
    }

    private static bool TryParseBooleanActionUrl(string url, string prefix, out string id, out bool accept)
    {
        id = "";
        accept = false;
        if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var remainder = url[prefix.Length..].Trim('/');
        var slashIndex = remainder.LastIndexOf('/');
        if (slashIndex <= 0 || slashIndex == remainder.Length - 1)
            return false;

        id = remainder[..slashIndex];
        var action = remainder[(slashIndex + 1)..];
        if (action.Equals("accept", StringComparison.OrdinalIgnoreCase))
        {
            accept = true;
            return true;
        }

        if (action.Equals("decline", StringComparison.OrdinalIgnoreCase))
            return true;

        id = "";
        return false;
    }

    private void HandlePartyInviteResponse(string inviteId, bool accept)
    {
        HandleNavigateToGame();
        _queueSocketService.AcceptPartyInviteAsync(inviteId, accept)
            .FireAndForget($"HandlePartyInviteResponse ({(accept ? "accept" : "decline")})");
    }

    private void HandleReadyCheckResponse(string roomId, bool accept)
    {
        HandleNavigateToGame();
        _queueSocketService.SetReadyCheckAsync(roomId, accept)
            .FireAndForget($"HandleReadyCheckResponse ({(accept ? "accept" : "decline")})");
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

    private void StartBackgroundVerificationIfNeeded()
    {
        if (_backgroundVerificationStarted ||
            CurrentContentViewModel is GameDownloadViewModel ||
            !HasValidGameDir())
        {
            return;
        }

        _backgroundVerificationStarted = true;
        _ = RunBackgroundVerificationAsync();
    }

    private void OnRemoteUpdatePollTick(object? sender, EventArgs e) =>
        CheckForRemoteGameUpdateAsync().FireAndForget("[RemoteUpdatePoll] tick");

    private void StartRemoteUpdatePollingIfReady()
    {
        if (_verifiedLocalManifestSnapshot == null || CurrentContentViewModel is not MainLauncherViewModel)
            return;

        _remoteUpdatePollTimer.Start();
        _ = CheckForRemoteGameUpdateAsync();
    }

    private void StopRemoteUpdatePolling() => _remoteUpdatePollTimer.Stop();

    private async Task CheckForRemoteGameUpdateAsync()
    {
        var localSnapshot = _verifiedLocalManifestSnapshot;
        if (localSnapshot == null ||
            CurrentContentViewModel is not MainLauncherViewModel launcherVm ||
            !HasValidGameDir())
        {
            return;
        }

        if (System.Threading.Interlocked.CompareExchange(ref _remoteUpdateCheckInFlight, 1, 0) != 0)
            return;

        try
        {
            var selectedDlcIds = _settingsStorage.Get().SelectedDlcIds ?? [];
            var remoteManifestSet = await _remoteManifestService.GetInstalledPackageManifestsAsync(selectedDlcIds);
            var hasUpdate = _manifestDiffService.ComputeFilesToDownload(
                remoteManifestSet.CombinedManifest,
                localSnapshot).Count > 0;

            Dispatcher.UIThread.Post(() => ApplyRemoteGameUpdateState(launcherVm, hasUpdate));
        }
        catch (Exception ex)
        {
            AppLog.Error("[RemoteUpdatePoll] Failed to check for remote game updates", ex);
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _remoteUpdateCheckInFlight, 0);
        }
    }

    private void ApplyRemoteGameUpdateState(MainLauncherViewModel launcherVm, bool hasUpdate)
    {
        if (CurrentContentViewModel != launcherVm)
            return;

        var wasPending = launcherVm.IsGameUpdatePending;
        launcherVm.SetGameUpdatePending(hasUpdate);

        if (!hasUpdate || wasPending)
            return;

        if (_windowService.IsWindowVisible && _windowService.IsWindowActive)
            return;

        _toastNotificationService.ShowGameUpdateAvailable();
    }

    public void Dispose()
    {
        _remoteUpdatePollTimer.Tick -= OnRemoteUpdatePollTick;
        _remoteUpdatePollTimer.Stop();
        _windowService.WindowShown -= OnWindowShown;
    }

    private async Task RunBackgroundVerificationAsync()
    {
        try
        {
            await Task.Delay(BackgroundVerificationDelay);
            Dispatcher.UIThread.Post(() =>
            {
                if (_verificationSatisfied || CurrentContentViewModel is not MainLauncherViewModel)
                    return;

                AppLog.Info("[BackgroundVerify] Starting full verification in background mode");
                AppState = AppState.VerifyingGame;
                EnterVerifyingGame(VerificationMode.Background);
            });
        }
        catch (Exception ex)
        {
            AppLog.Error("[BackgroundVerify] Failed to start background verification", ex);
        }
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
