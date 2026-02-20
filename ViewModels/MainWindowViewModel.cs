using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
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

    [ObservableProperty]
    private SteamStatus _steamStatus;

    [ObservableProperty]
    private object? _currentContentViewModel;

    public bool IsSteamRunning => SteamStatus is SteamStatus.Running or SteamStatus.Offline;

    public MainWindowViewModel(
        SteamManager steamManager,
        ISettingsStorage settingsStorage,
        ISteamAuthApi steamAuthApi,
        IBackendApiService backendApiService,
        IQueueSocketService queueSocketService)
    {
        _steamManager = steamManager;
        _settingsStorage = settingsStorage;
        _steamAuthApi = steamAuthApi;
        _backendApiService = backendApiService;
        _queueSocketService = queueSocketService;
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
    }

    private void UpdateContentViewModel()
    {
        (CurrentContentViewModel as IDisposable)?.Dispose();
        CurrentContentViewModel = SteamStatus == SteamStatus.NotRunning
            ? new LaunchSteamFirstViewModel()
            : new MainLauncherViewModel(_steamManager, _settingsStorage, _steamAuthApi, _backendApiService, _queueSocketService);
    }
}
