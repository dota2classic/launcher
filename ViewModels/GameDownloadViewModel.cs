using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Models;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

public partial class GameDownloadViewModel : ViewModelBase
{
    internal const string ManifestUrl = "https://launcher.dotaclassic.ru/files/manifest.json";

    private readonly ILocalManifestService _localManifestService;
    private readonly IManifestDiffService _manifestDiffService;
    private readonly IGameDownloadService _gameDownloadService;

    public string GameDirectory { get; set; } = "";
    public Action? OnCompleted { get; set; }

    /// <summary>
    /// When true, <see cref="RunAsync"/> will pause and show the Windows Defender
    /// exclusion confirmation modal before starting the scan/download.
    /// </summary>
    public bool NeedDefenderModal { get; set; }

    /// <summary>
    /// Called after the user responds to the Defender modal (accept or skip).
    /// Used by the parent ViewModel to persist the decision to settings.
    /// </summary>
    public Action? OnDefenderDecisionMade { get; set; }

    /// <summary>
    /// Optional pre-started task for the remote manifest fetch. When set,
    /// <see cref="RunAsync"/> awaits it instead of making its own HTTP request,
    /// eliminating the "Подключение к серверу..." phase.
    /// Consumed on first use; retry always fetches fresh.
    /// </summary>
    public Task<GameManifest?>? PrefetchedRemoteManifest { get; set; }

    private TaskCompletionSource? _defenderTcs;

    [ObservableProperty] private bool _showDefenderModal;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _detailsText = "";
    [ObservableProperty] private string _currentFileText = "";
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private bool _isIndeterminate = true;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorText = "";

    public GameDownloadViewModel(
        ILocalManifestService localManifestService,
        IManifestDiffService manifestDiffService,
        IGameDownloadService gameDownloadService)
    {
        _localManifestService = localManifestService;
        _manifestDiffService = manifestDiffService;
        _gameDownloadService = gameDownloadService;
    }

    public void StartAsync() => _ = RunAsync();

    [RelayCommand]
    private async Task AcceptDefenderAsync()
    {
        ShowDefenderModal = false;
        OnDefenderDecisionMade?.Invoke();
        await WindowsDefenderService.TryAddExclusionAsync(GameDirectory);
        _defenderTcs?.TrySetResult();
    }

    [RelayCommand]
    private void SkipDefender()
    {
        ShowDefenderModal = false;
        OnDefenderDecisionMade?.Invoke();
        _defenderTcs?.TrySetResult();
    }

    [RelayCommand]
    private void Retry()
    {
        HasError = false;
        ErrorText = "";
        StatusText = "";
        IsIndeterminate = true;
        ProgressValue = 0;
        DetailsText = "";
        CurrentFileText = "";
        StartAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            // Phase 0: Ask the user about Windows Defender exclusion (first time only)
            if (NeedDefenderModal)
            {
                _defenderTcs = new TaskCompletionSource();
                ShowDefenderModal = true;
                await _defenderTcs.Task;
            }

            // Phase 1: Fetch remote manifest (use pre-started task if available)
            IsIndeterminate = true;
            DetailsText = "";

            GameManifest remote;
            var prefetchTask = PrefetchedRemoteManifest;
            PrefetchedRemoteManifest = null; // consume; retry will fetch fresh

            if (prefetchTask != null)
            {
                GameManifest? prefetched = null;
                try { prefetched = await prefetchTask; } catch { }

                if (prefetched != null)
                {
                    remote = prefetched;
                }
                else
                {
                    // Prefetch failed — fall back to a fresh request
                    StatusText = "Подключение к серверу...";
                    using var http = new HttpClient();
                    var json = await http.GetStringAsync(ManifestUrl);
                    remote = JsonSerializer.Deserialize<GameManifest>(json)!;
                }
            }
            else
            {
                StatusText = "Подключение к серверу...";
                using var http = new HttpClient();
                var json = await http.GetStringAsync(ManifestUrl);
                remote = JsonSerializer.Deserialize<GameManifest>(json)!;
            }

            // Phase 2: Scan local files
            IsIndeterminate = false;
            StatusText = "Проверка файлов...";

            var scanProgress = new Progress<(int done, int total)>(p =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ProgressValue = p.total > 0 ? p.done * 100.0 / p.total : 0;
                    StatusText = $"Проверка файлов... ({p.done}/{p.total})";
                });
            });

            var local = await _localManifestService.BuildAsync(GameDirectory, scanProgress);

            // Phase 3: Compute diff
            var toDownload = _manifestDiffService.ComputeFilesToDownload(remote, local);
            int totalRemoteFiles = remote.Files.Count;
            int alreadyOkFiles = totalRemoteFiles - toDownload.Count;

            if (toDownload.Count == 0)
            {
                StatusText = "Игра обновлена";
                DetailsText = "";
                ProgressValue = 100;
                await Task.Delay(500); // brief pause so user sees "up to date"
                Dispatcher.UIThread.Post(() => OnCompleted?.Invoke());
                return;
            }

            // Phase 4: Download
            // Progress bar reflects global state: already-ok bytes + downloaded this session / total remote bytes.
            // This way a restart mid-download shows a non-empty bar instead of starting from 0.
            var totalRemoteBytes = remote.Files.Sum(f => f.Size);
            var totalBytes = 0L;
            foreach (var f in toDownload) totalBytes += f.Size;
            var alreadyOkBytes = totalRemoteBytes - totalBytes;

            StatusText = $"Загрузка ({toDownload.Count} файлов)...";
            ProgressValue = totalRemoteBytes > 0 ? alreadyOkBytes * 100.0 / totalRemoteBytes : 0;
            DetailsText = FormatSize(totalBytes) + " всего";

            var downloadProgress = new Progress<DownloadProgress>(p =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ProgressValue = totalRemoteBytes > 0
                        ? (alreadyOkBytes + p.BytesDownloaded) * 100.0 / totalRemoteBytes
                        : 0;

                    var speed = FormatSpeed(p.SpeedBytesPerSec);
                    var remaining = p.TotalBytes - p.BytesDownloaded;
                    var etaStr = p.SpeedBytesPerSec > 0
                        ? FormatEta(remaining / p.SpeedBytesPerSec)
                        : "";

                    StatusText = $"Загрузка ({alreadyOkFiles + p.FilesDownloaded}/{totalRemoteFiles} файлов)";
                    CurrentFileText = p.CurrentFile;
                    DetailsText = $"{FormatSize(p.BytesDownloaded)} / {FormatSize(p.TotalBytes)}  {speed}{(etaStr.Length > 0 ? "  ~" + etaStr : "")}";
                });
            });

            await _gameDownloadService.DownloadFilesAsync(toDownload, GameDirectory, downloadProgress);

            StatusText = "Готово!";
            DetailsText = "";
            CurrentFileText = "";
            ProgressValue = 100;
            await Task.Delay(500);
            Dispatcher.UIThread.Post(() => OnCompleted?.Invoke());
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                HasError = true;
                StatusText = "Ошибка загрузки";
                ErrorText = ex.Message;
                IsIndeterminate = false;
            });
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} ГБ";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} МБ";
        if (bytes >= 1024) return $"{bytes / 1024.0:F1} КБ";
        return $"{bytes} Б";
    }

    private static string FormatSpeed(double bytesPerSec)
    {
        if (bytesPerSec <= 0) return "";
        if (bytesPerSec >= 1_048_576) return $"{bytesPerSec / 1_048_576.0:F1} МБ/с";
        if (bytesPerSec >= 1024) return $"{bytesPerSec / 1024.0:F1} КБ/с";
        return $"{bytesPerSec:F0} Б/с";
    }

    private static string FormatEta(double seconds)
    {
        if (seconds <= 0) return "";
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}ч {ts.Minutes}м";
        if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}м {ts.Seconds}с";
        return $"{ts.Seconds}с";
    }
}
