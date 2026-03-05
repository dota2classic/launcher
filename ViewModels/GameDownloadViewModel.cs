using System;
using System.Collections.Generic;
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
    private readonly RedistInstallService _redistInstallService;

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

    [ObservableProperty] private VerificationPhase _phase = VerificationPhase.FetchingManifest;
    [ObservableProperty] private bool _showDefenderModal;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _detailsText = "";
    [ObservableProperty] private string _currentFileText = "";
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private bool _isIndeterminate = false;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorText = "";

    public GameDownloadViewModel(
        ILocalManifestService localManifestService,
        IManifestDiffService manifestDiffService,
        IGameDownloadService gameDownloadService,
        RedistInstallService redistInstallService)
    {
        _localManifestService = localManifestService;
        _manifestDiffService = manifestDiffService;
        _gameDownloadService = gameDownloadService;
        _redistInstallService = redistInstallService;
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
            await RunDefenderPhaseAsync();
            var remote = await FetchManifestAsync();
            var local = await ScanLocalFilesAsync();
            var toDownload = ComputeDiff(remote, local,
                out int totalRemoteFiles, out long totalRemoteBytes, out long alreadyOkBytes);

            if (toDownload.Count == 0)
            {
                SetPhase(VerificationPhase.Complete, "Игра обновлена", progress: 100);
                await Task.Delay(500);
                await InstallRedistAsync();
                Dispatcher.UIThread.Post(() => OnCompleted?.Invoke());
                return;
            }

            await DownloadFilesAsync(toDownload, totalRemoteFiles, totalRemoteBytes, alreadyOkBytes);

            SetPhase(VerificationPhase.Complete, "Готово!", progress: 100);
            await Task.Delay(500);
            await InstallRedistAsync();
            Dispatcher.UIThread.Post(() => OnCompleted?.Invoke());
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Phase = VerificationPhase.Failed;
                HasError = true;
                StatusText = "Ошибка загрузки";
                ErrorText = ex.Message;
                IsIndeterminate = false;
            });
        }
    }

    // ── Phases ───────────────────────────────────────────────────────────────

    private async Task RunDefenderPhaseAsync()
    {
        if (!NeedDefenderModal)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            Phase = VerificationPhase.AwaitingDefender;
            ShowDefenderModal = true;
        });
        _defenderTcs = new TaskCompletionSource();
        await _defenderTcs.Task;
    }

    private async Task<GameManifest> FetchManifestAsync()
    {
        var prefetchTask = PrefetchedRemoteManifest;
        PrefetchedRemoteManifest = null; // consume; retry will fetch fresh

        if (prefetchTask != null)
        {
            GameManifest? prefetched = null;
            try { prefetched = await prefetchTask; } catch { }

            if (prefetched != null)
                return prefetched;
        }

        // No prefetch or it failed — fetch fresh
        SetPhase(VerificationPhase.FetchingManifest, "Подключение к серверу...");
        using var http = new HttpClient();
        var json = await http.GetStringAsync(ManifestUrl);
        return JsonSerializer.Deserialize<GameManifest>(json)!;
    }

    private async Task<GameManifest> ScanLocalFilesAsync()
    {
        SetPhase(VerificationPhase.ScanningFiles, "Проверка файлов...", progress: 0, indeterminate: false);

        var scanProgress = new Progress<(int done, int total)>(p =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProgressValue = p.total > 0 ? p.done * 100.0 / p.total : 0;
                StatusText = $"Проверка файлов... ({p.done}/{p.total})";
            });
        });

        return await _localManifestService.BuildAsync(GameDirectory, scanProgress);
    }

    private IReadOnlyList<GameManifestFile> ComputeDiff(
        GameManifest remote,
        GameManifest local,
        out int totalRemoteFiles,
        out long totalRemoteBytes,
        out long alreadyOkBytes)
    {
        Dispatcher.UIThread.Post(() => Phase = VerificationPhase.ComputingDiff);

        var toDownload = _manifestDiffService.ComputeFilesToDownload(remote, local);
        totalRemoteFiles = remote.Files.Count;
        totalRemoteBytes = remote.Files.Sum(f => f.Size);
        alreadyOkBytes = totalRemoteBytes - toDownload.Sum(f => f.Size);

        return toDownload;
    }

    private async Task DownloadFilesAsync(
        IReadOnlyList<GameManifestFile> toDownload,
        int totalRemoteFiles,
        long totalRemoteBytes,
        long alreadyOkBytes)
    {
        int alreadyOkFiles = totalRemoteFiles - toDownload.Count;
        long totalBytes = toDownload.Sum(f => f.Size);

        SetPhase(VerificationPhase.Downloading,
            $"Загрузка ({toDownload.Count} файлов)...",
            details: FormatSize(totalBytes) + " всего",
            progress: totalRemoteBytes > 0 ? alreadyOkBytes * 100.0 / totalRemoteBytes : 0,
            indeterminate: false);

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

        Dispatcher.UIThread.Post(() =>
        {
            DetailsText = "";
            CurrentFileText = "";
        });
    }

    private async Task InstallRedistAsync()
    {
        SetPhase(VerificationPhase.InstallingRedist, "Установка компонентов...", indeterminate: true);

        var redistProgress = new Progress<string>(name =>
        {
            Dispatcher.UIThread.Post(() => CurrentFileText = name);
        });

        await _redistInstallService.InstallAsync(GameDirectory, redistProgress);

        Dispatcher.UIThread.Post(() =>
        {
            IsIndeterminate = false;
            CurrentFileText = "";
            DetailsText = "";
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SetPhase(
        VerificationPhase phase,
        string? status = null,
        string? details = null,
        double? progress = null,
        bool? indeterminate = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Phase = phase;
            if (status != null) StatusText = status;
            if (details != null) DetailsText = details;
            if (progress.HasValue) ProgressValue = progress.Value;
            if (indeterminate.HasValue) IsIndeterminate = indeterminate.Value;
        });
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
