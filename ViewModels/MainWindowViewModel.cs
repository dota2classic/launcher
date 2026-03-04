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
    private readonly UpdateService _updateService;
    private readonly ILocalManifestService _localManifestService;
    private readonly IManifestDiffService _manifestDiffService;
    private readonly IGameDownloadService _gameDownloadService;

    [ObservableProperty]
    private SteamStatus _steamStatus;

    [ObservableProperty]
    private object? _currentContentViewModel;

    [ObservableProperty]
    private bool _updateAvailable;

    public bool IsSteamRunning => SteamStatus is SteamStatus.Running or SteamStatus.Offline;

    public string WindowTitle { get; } =
        $"D2C Launcher v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?"}";

    public MainWindowViewModel(
        SteamManager steamManager,
        ISettingsStorage settingsStorage,
        IGameLaunchSettingsStorage launchSettingsStorage,
        ISteamAuthApi steamAuthApi,
        IBackendApiService backendApiService,
        IQueueSocketService queueSocketService,
        ICvarSettingsProvider cvarProvider,
        UpdateService updateService,
        ILocalManifestService localManifestService,
        IManifestDiffService manifestDiffService,
        IGameDownloadService gameDownloadService)
    {
        _steamManager = steamManager;
        _settingsStorage = settingsStorage;
        _launchSettingsStorage = launchSettingsStorage;
        _steamAuthApi = steamAuthApi;
        _backendApiService = backendApiService;
        _queueSocketService = queueSocketService;
        _cvarProvider = cvarProvider;
        _updateService = updateService;
        _localManifestService = localManifestService;
        _manifestDiffService = manifestDiffService;
        _gameDownloadService = gameDownloadService;
        _steamStatus = steamManager.SteamStatus;
        UpdateContentViewModel();

        _steamManager.OnSteamStatusUpdated += status =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                SteamStatus = status;
                OnPropertyChanged(nameof(IsSteamRunning));
                UpdateContentViewModel();
            });
        };

        _steamManager.OnUserUpdated += _ =>
        {
            Dispatcher.UIThread.Post(UpdateContentViewModel);
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

    private void ShowMainLauncher()
    {
        (CurrentContentViewModel as System.IDisposable)?.Dispose();
        CurrentContentViewModel = new MainLauncherViewModel(
            _steamManager, _settingsStorage, _launchSettingsStorage, _cvarProvider,
            _steamAuthApi, _backendApiService, _queueSocketService);
    }

    private void ShowGameDownload(string gameDir)
    {
        (CurrentContentViewModel as System.IDisposable)?.Dispose();
        var vm = new GameDownloadViewModel(_localManifestService, _manifestDiffService, _gameDownloadService)
        {
            GameDirectory = gameDir,
            OnCompleted = () => Dispatcher.UIThread.Post(ShowMainLauncher)
        };
        CurrentContentViewModel = vm;
        vm.StartAsync();
    }

    private void UpdateContentViewModel()
    {
        var steamOk = SteamStatus == SteamStatus.Running && _steamManager.CurrentUser != null;

        if (steamOk)
        {
            var gameDir = _settingsStorage.Get().GameDirectory;

            if (string.IsNullOrWhiteSpace(gameDir))
            {
                // Already showing the picker — don't recreate it.
                if (CurrentContentViewModel is SelectGameViewModel)
                    return;

                (CurrentContentViewModel as System.IDisposable)?.Dispose();

                var selectVm = new SelectGameViewModel();
                selectVm.GameDirectorySelected = path =>
                {
                    var settings = _settingsStorage.Get();
                    settings.GameDirectory = path;
                    _settingsStorage.Save(settings);
                    Dispatcher.UIThread.Post(() => ShowGameDownload(path));
                };
                CurrentContentViewModel = selectVm;
                return;
            }

            // Game directory is set — go through download/verify before showing the main launcher.
            // Guard: don't restart the download if already in progress or on main screen.
            if (CurrentContentViewModel is GameDownloadViewModel or MainLauncherViewModel)
                return;

            ShowGameDownload(gameDir);
            return;
        }

        (CurrentContentViewModel as System.IDisposable)?.Dispose();

        CurrentContentViewModel = SteamStatus switch
        {
            SteamStatus.Running =>
                new LaunchSteamFirstViewModel("Подключение к Steam..."),
            SteamStatus.Offline =>
                new LaunchSteamFirstViewModel("Войдите в аккаунт Steam."),
            _ =>
                new LaunchSteamFirstViewModel()
        };
    }
}
