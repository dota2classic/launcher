using System;
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
    private readonly ISteamAuthApi _steamAuthApi;
    private readonly IBackendApiService _backendApiService;
    private readonly IQueueSocketService _queueSocketService;
    private readonly UpdateService _updateService;

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
        ISteamAuthApi steamAuthApi,
        IBackendApiService backendApiService,
        IQueueSocketService queueSocketService,
        UpdateService updateService)
    {
        _steamManager = steamManager;
        _settingsStorage = settingsStorage;
        _steamAuthApi = steamAuthApi;
        _backendApiService = backendApiService;
        _queueSocketService = queueSocketService;
        _updateService = updateService;
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

                (CurrentContentViewModel as IDisposable)?.Dispose();

                var selectVm = new SelectGameViewModel();
                selectVm.GameDirectorySelected = path =>
                {
                    var settings = _settingsStorage.Get();
                    settings.GameDirectory = path;
                    _settingsStorage.Save(settings);
                    Dispatcher.UIThread.Post(UpdateContentViewModel);
                };
                CurrentContentViewModel = selectVm;
                return;
            }

            // Game directory is set — show (or keep) the main launcher.
            if (CurrentContentViewModel is MainLauncherViewModel)
                return;

            (CurrentContentViewModel as IDisposable)?.Dispose();
            CurrentContentViewModel = new MainLauncherViewModel(
                _steamManager, _settingsStorage, _steamAuthApi, _backendApiService, _queueSocketService);
            return;
        }

        (CurrentContentViewModel as IDisposable)?.Dispose();

        CurrentContentViewModel = SteamStatus switch
        {
            SteamStatus.Running =>
                new LaunchSteamFirstViewModel("Connecting to Steam..."),
            SteamStatus.Offline =>
                new LaunchSteamFirstViewModel("Please log in to Steam."),
            _ =>
                new LaunchSteamFirstViewModel()
        };
    }
}
