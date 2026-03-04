using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
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
    private bool _manifestDiffStarted;

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
        IManifestDiffService manifestDiffService)
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

    private async Task RunManifestDiffAsync(string gameDir)
    {
        try
        {
            Console.WriteLine("[Manifest] Fetching remote manifest...");
            using var http = new HttpClient();
            var json = await http.GetStringAsync("https://launcher.dotaclassic.ru/files/manifest.json");
            var remote = JsonSerializer.Deserialize<GameManifest>(json)!;
            Console.WriteLine($"[Manifest] Remote: {remote.Files.Count} files");

            Console.WriteLine("[Manifest] Building local manifest (this may take a while)...");
            var progress = new Progress<(int done, int total)>(p =>
            {
                if (p.done % 2000 == 0 || p.done == p.total)
                    Console.WriteLine($"[Manifest] Scanned {p.done}/{p.total}");
            });
            var local = await _localManifestService.BuildAsync(gameDir, progress);
            Console.WriteLine($"[Manifest] Local: {local.Files.Count} files");

            var toDownload = _manifestDiffService.ComputeFilesToDownload(remote, local);
            var totalBytes = toDownload.Sum(f => f.Size);
            Console.WriteLine($"[Manifest] To download: {toDownload.Count} files, {totalBytes / 1024.0 / 1024.0:F1} MB");
            foreach (var file in toDownload.Take(20))
                Console.WriteLine($"  {file.Mode.PadRight(8)} {file.Path} ({file.Size / 1024.0:F1} KB)");
            if (toDownload.Count > 20)
                Console.WriteLine($"  ... and {toDownload.Count - 20} more");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Manifest] Error: {ex}");
        }
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
                _steamManager, _settingsStorage, _launchSettingsStorage, _cvarProvider, _steamAuthApi, _backendApiService, _queueSocketService);

            if (!_manifestDiffStarted)
            {
                _manifestDiffStarted = true;
                _ = RunManifestDiffAsync(gameDir);
            }
            return;
        }

        (CurrentContentViewModel as IDisposable)?.Dispose();

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
