using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Integration;
using d2c_launcher.Models;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SteamManager _steamManager;
    private readonly ISettingsStorage _settingsStorage;
    private readonly IGameLaunchSettingsStorage _launchSettingsStorage;
    private readonly ISteamAuthApi _steamAuthApi;
    private readonly IBackendApiService _backendApiService;
    private readonly IQueueSocketService _queueSocketService;
    private readonly ICvarSettingsProvider _cvarProvider;
    private readonly IVideoSettingsProvider _videoProvider;
    private readonly UpdateService _updateService;
    private readonly ILocalManifestService _localManifestService;
    private readonly IManifestDiffService _manifestDiffService;
    private readonly IGameDownloadService _gameDownloadService;
    private readonly RedistInstallService _redistInstallService;
    private readonly IContentRegistryService _registryService;

    [ObservableProperty]
    private AppState _appState;

    [ObservableProperty]
    private object? _currentContentViewModel;

    [ObservableProperty]
    private bool _updateAvailable;

    public bool IsSteamRunning => _steamManager.SteamStatus is SteamStatus.Running or SteamStatus.Offline;

    public string WindowTitle { get; } =
        $"dotaclassic v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?"}";

    public MainWindowViewModel(
        SteamManager steamManager,
        ISettingsStorage settingsStorage,
        IGameLaunchSettingsStorage launchSettingsStorage,
        ISteamAuthApi steamAuthApi,
        IBackendApiService backendApiService,
        IQueueSocketService queueSocketService,
        ICvarSettingsProvider cvarProvider,
        IVideoSettingsProvider videoProvider,
        UpdateService updateService,
        ILocalManifestService localManifestService,
        IManifestDiffService manifestDiffService,
        IGameDownloadService gameDownloadService,
        RedistInstallService redistInstallService,
        IContentRegistryService registryService)
    {
        _steamManager = steamManager;
        _settingsStorage = settingsStorage;
        _launchSettingsStorage = launchSettingsStorage;
        _steamAuthApi = steamAuthApi;
        _backendApiService = backendApiService;
        _queueSocketService = queueSocketService;
        _cvarProvider = cvarProvider;
        _videoProvider = videoProvider;
        _updateService = updateService;
        _localManifestService = localManifestService;
        _manifestDiffService = manifestDiffService;
        _gameDownloadService = gameDownloadService;
        _redistInstallService = redistInstallService;
        _registryService = registryService;

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
        var hasUpdate = await _updateService.CheckAndDownloadAsync();
        if (hasUpdate)
            Dispatcher.UIThread.Post(() => UpdateAvailable = true);
    }

    [RelayCommand]
    private void ApplyUpdate() => _updateService.ApplyAndRestart();

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
                CurrentContentViewModel = new LaunchSteamFirstViewModel
                {
                    TryAgainCallback = TransitionOnSteamUpdate
                };
                break;

            case AppState.SelectGameDirectory:
                // Guard: already showing — don't recreate
                if (CurrentContentViewModel is SelectGameViewModel)
                    return;
                DisposeCurrentVm();
                var settings = _settingsStorage.Get();
                var selectVm = new SelectGameViewModel(_registryService)
                {
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
        };
        CurrentContentViewModel = vm;
        vm.StartAsync();
    }

    private void EnterLauncher()
    {
        DisposeCurrentVm();
        var vm = new MainLauncherViewModel(
            _steamManager, _settingsStorage, _launchSettingsStorage, _cvarProvider, _videoProvider,
            _steamAuthApi, _backendApiService, _queueSocketService, _registryService);
        vm.OnGameDirectoryChanged = _ => Dispatcher.UIThread.Post(() => EnterState(AppStateMachine.OnGameDirChanged(AppState)));
        vm.OnDlcChanged = removedIds => Dispatcher.UIThread.Post(() =>
        {
            AppState = AppState.VerifyingGame;
            EnterVerifyingGame(removedIds);
        });
        CurrentContentViewModel = vm;
    }

    private void DisposeCurrentVm()
    {
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
