using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Resources;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class GameDownloadViewModel : ViewModelBase
{
    private const string BaseManifestUrl = "https://launcher.dotaclassic.ru/files/";

    private readonly IContentRegistryService _registryService;
    private readonly ILocalManifestService _localManifestService;
    private readonly IManifestDiffService _manifestDiffService;
    private readonly IGameDownloadService _gameDownloadService;
    private readonly RedistInstallService _redistInstallService;

    public string GameDirectory { get; set; } = "";

    /// <summary>
    /// IDs of optional DLC packages the user has chosen to install.
    /// Null or empty means only required packages are downloaded.
    /// </summary>
    public List<string>? SelectedDlcIds { get; set; }

    /// <summary>
    /// IDs of ALL packages that were previously installed (required + optional).
    /// Used to detect which optional packages have been deselected and whose files
    /// should be removed before the next verification pass.
    /// </summary>
    public List<string>? InstalledPackageIds { get; set; }

    public Action? OnCompleted { get; set; }

    /// <summary>
    /// Called when the selected game directory is not a valid Dota 2 Classic installation.
    /// The caller should clear the stored game directory from settings and navigate back to folder selection.
    /// The string argument is an optional error message to display on the folder selection screen.
    /// </summary>
    public Action<string?>? OnInvalidGameDirectory { get; set; }

    /// <summary>
    /// Called after a successful download with the IDs of all packages that were installed
    /// (required + selected optional). Use this to persist install state to settings.
    /// </summary>
    public Action<List<string>>? OnPackagesInstalled { get; set; }

    private List<string> _pendingInstalledPackageIds = [];

    /// <summary>
    /// When true, <see cref="RunAsync"/> will pause and show the Windows Defender
    /// exclusion confirmation modal before starting the scan/download.
    /// </summary>
    public bool NeedDefenderModal { get; set; }

    /// <summary>
    /// Called after the user responds to the Defender modal.
    /// The bool argument is true if the user accepted (exclusion was requested),
    /// false if the user skipped (no exclusion added).
    /// </summary>
    public Action<bool>? OnDefenderDecisionMade { get; set; }

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
        IContentRegistryService registryService,
        ILocalManifestService localManifestService,
        IManifestDiffService manifestDiffService,
        IGameDownloadService gameDownloadService,
        RedistInstallService redistInstallService)
    {
        _registryService = registryService;
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
        OnDefenderDecisionMade?.Invoke(true);
        await WindowsDefenderService.TryAddExclusionAsync(GameDirectory);
        _defenderTcs?.TrySetResult();
    }

    [RelayCommand]
    private void SkipDefender()
    {
        ShowDefenderModal = false;
        OnDefenderDecisionMade?.Invoke(false);
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
        AppLog.Info($"[GameDownload] RunAsync started. GameDirectory={GameDirectory}");
        if (!GameDirectoryValidator.IsAcceptable(GameDirectory, out var dirError))
        {
            Dispatcher.UIThread.Post(() =>
            {
                Phase = VerificationPhase.Failed;
                HasError = true;
                StatusText = Strings.InvalidGameFolder;
                ErrorText = dirError ?? Strings.FolderNotDotaclassic;
                IsIndeterminate = false;
                OnInvalidGameDirectory?.Invoke(dirError);
            });
            return;
        }

        try
        {
            await RunDefenderPhaseAsync();
            await DeleteRemovedPackagesAsync();
            var packages = await FetchAllPackageManifestsAsync();
            var local = await ScanLocalFilesAsync();

            bool anyDownloaded = false;
            foreach (var (_, pkgManifest) in packages)
            {
                var toDownload = ComputeDiff(pkgManifest, local,
                    out int totalRemoteFiles, out long totalRemoteBytes, out long alreadyOkBytes);

                if (toDownload.Count == 0)
                    continue;

                anyDownloaded = true;
                await DownloadFilesAsync(toDownload, totalRemoteFiles, totalRemoteBytes, alreadyOkBytes);
            }

            SetPhase(VerificationPhase.Complete, anyDownloaded ? Strings.Done : Strings.GameUpdated, progress: 100);
            await Task.Delay(500);
            await InstallRedistAsync();
            Dispatcher.UIThread.Post(() =>
            {
                OnPackagesInstalled?.Invoke(_pendingInstalledPackageIds);
                OnCompleted?.Invoke();
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.Error($"[GameDownload] Access denied for game directory '{GameDirectory}'", ex);
            Dispatcher.UIThread.Post(() =>
            {
                Phase = VerificationPhase.Failed;
                HasError = true;
                StatusText = Strings.NoFolderAccessTitle;
                ErrorText = ex.Message;
                IsIndeterminate = false;
                OnInvalidGameDirectory?.Invoke(Strings.NoFolderAccess);
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Phase = VerificationPhase.Failed;
                HasError = true;
                StatusText = Strings.LoadingError;
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

    private async Task DeleteRemovedPackagesAsync()
    {
        if (InstalledPackageIds == null || InstalledPackageIds.Count == 0)
            return;

        var registry = await _registryService.GetAsync();
        if (registry == null) return;

        var requiredIds = registry.Packages
            .Where(p => !p.Optional)
            .Select(p => p.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var wantedIds = requiredIds
            .Concat(SelectedDlcIds ?? [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var idsToRemove = InstalledPackageIds
            .Where(id => !wantedIds.Contains(id))
            .ToList();

        if (idsToRemove.Count == 0)
            return;

        SetPhase(VerificationPhase.FetchingManifest, Strings.DeletingDlcFiles, indeterminate: true);

        using var http = new HttpClient();

        foreach (var id in idsToRemove)
        {
            var pkg = registry.Packages.FirstOrDefault(p => p.Id == id);
            if (pkg == null) continue;

            Dispatcher.UIThread.Post(() => CurrentFileText = pkg.Name);

            var manifestUrl = $"{BaseManifestUrl}{pkg.Folder}/manifest.json";
            string json;
            try { json = await http.GetStringAsync(manifestUrl); }
            catch { continue; }

            var pkgManifest = JsonSerializer.Deserialize<GameManifest>(json);
            if (pkgManifest == null) continue;

            await Task.Run(() =>
            {
                foreach (var file in pkgManifest.Files)
                {
                    var localPath = System.IO.Path.Combine(GameDirectory, file.Path);
                    try
                    {
                        if (System.IO.File.Exists(localPath))
                            System.IO.File.Delete(localPath);
                    }
                    catch { /* skip locked/inaccessible files */ }
                }
            });
        }

        Dispatcher.UIThread.Post(() => CurrentFileText = "");
    }

    private async Task<List<(ContentPackage Package, GameManifest Manifest)>> FetchAllPackageManifestsAsync()
    {
        SetPhase(VerificationPhase.FetchingManifest, Strings.ConnectingToServer, indeterminate: true);

        var registry = await _registryService.GetAsync();

        if (registry == null || registry.Packages.Count == 0)
            throw new Exception(Strings.FailedToLoadPackages);

        // Determine which packages to download: all required + user-selected optional
        var packagesToInstall = registry.Packages
            .Where(p => !p.Optional || (SelectedDlcIds != null && SelectedDlcIds.Contains(p.Id)))
            .ToList();

        _pendingInstalledPackageIds = packagesToInstall.Select(p => p.Id).ToList();

        SetPhase(VerificationPhase.FetchingPackageManifests,
            $"Загрузка манифестов ({packagesToInstall.Count} пакетов)...", indeterminate: true);

        var result = new List<(ContentPackage, GameManifest)>();
        using var http = new HttpClient();

        foreach (var pkg in packagesToInstall)
        {
            Dispatcher.UIThread.Post(() => CurrentFileText = pkg.Name);

            var manifestUrl = $"{BaseManifestUrl}{pkg.Folder}/manifest.json";
            var json = await http.GetStringAsync(manifestUrl);
            var pkgManifest = JsonSerializer.Deserialize<GameManifest>(json)
                ?? throw new Exception($"Манифест пакета {pkg.Name} пустой.");

            // Annotate each file with its package folder for download URL construction
            foreach (var file in pkgManifest.Files)
            {
                file.PackageFolder = pkg.Folder;
                file.PackageName = pkg.Name;
            }

            result.Add((pkg, pkgManifest));
        }

        Dispatcher.UIThread.Post(() => CurrentFileText = "");

        return result;
    }

    private async Task<GameManifest> ScanLocalFilesAsync()
    {
        SetPhase(VerificationPhase.ScanningFiles, Strings.VerifyingFiles, progress: 0, indeterminate: false);

        var scanProgress = new Progress<(int done, int total)>(p =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProgressValue = p.total > 0 ? p.done * 100.0 / p.total : 0;
                StatusText = $"{Strings.VerifyingFiles} ({p.done}/{p.total})";
            });
        });

        var sw = Stopwatch.StartNew();
        var manifest = await _localManifestService.BuildAsync(GameDirectory, scanProgress);
        sw.Stop();

        FaroTelemetryService.TrackEvent("scan_completed", new Dictionary<string, string>
        {
            ["duration_ms"] = sw.ElapsedMilliseconds.ToString(),
            ["file_count"] = manifest.Files.Count.ToString(),
        });

        return manifest;
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
            $"Загрузка (0/{toDownload.Count} файлов)...",
            details: $"0 / {FormatSize(totalBytes)}",
            progress: 0,
            indeterminate: false);

        var downloadProgress = new Progress<DownloadProgress>(p =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProgressValue = totalBytes > 0
                    ? p.BytesDownloaded * 100.0 / totalBytes
                    : 0;

                var speed = FormatSpeed(p.SpeedBytesPerSec);
                var remaining = p.TotalBytes - p.BytesDownloaded;
                var etaStr = p.SpeedBytesPerSec > 0
                    ? FormatEta(remaining / p.SpeedBytesPerSec)
                    : "";

                StatusText = $"Загрузка ({p.FilesDownloaded}/{toDownload.Count} файлов){(string.IsNullOrEmpty(p.CurrentPackageName) ? "" : $" — {p.CurrentPackageName}")}";
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
        SetPhase(VerificationPhase.InstallingRedist, Strings.InstallingComponents, indeterminate: true);

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
