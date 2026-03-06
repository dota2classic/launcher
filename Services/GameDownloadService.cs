using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public class GameDownloadService : IGameDownloadService
{
    private const string BaseUrl = "https://launcher.dotaclassic.ru/files/";
    private const int ChunkSize = 262144; // 256 KB
    private const int MaxConcurrency = 16;
    private const int MaxFileAttempts = 5;
    private static readonly TimeSpan SpeedWindow = TimeSpan.FromSeconds(3);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public async Task DownloadFilesAsync(
        IReadOnlyList<GameManifestFile> files,
        string gameDirectory,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var totalBytes = 0L;
        foreach (var f in files)
            totalBytes += f.Size;

        long bytesDownloaded = 0;
        int filesDownloaded = 0;

        // Rolling speed window — guarded by speedLock for thread safety
        var speedSamples = new Queue<(long Ticks, long Bytes)>();
        var speedWindowBytes = 0L;
        var speedLock = new object();
        var sw = Stopwatch.StartNew();

        void AddSample(long bytes)
        {
            lock (speedLock)
            {
                var now = sw.ElapsedTicks;
                speedSamples.Enqueue((now, bytes));
                speedWindowBytes += bytes;

                var cutoff = now - (long)(SpeedWindow.TotalSeconds * Stopwatch.Frequency);
                while (speedSamples.Count > 0 && speedSamples.Peek().Ticks < cutoff)
                    speedWindowBytes -= speedSamples.Dequeue().Bytes;
            }
        }

        double GetSpeedBytesPerSec()
        {
            lock (speedLock)
            {
                if (speedSamples.Count < 2) return 0;
                var oldest = speedSamples.Peek().Ticks;
                var elapsed = (sw.ElapsedTicks - oldest) / (double)Stopwatch.Frequency;
                return elapsed > 0 ? speedWindowBytes / elapsed : 0;
            }
        }

        // Throttle UI reports to ~20/sec — avoid flooding the UI thread with 30k+ posts
        long lastReportTick = 0;
        var reportInterval = Stopwatch.Frequency / 20; // 50 ms

        void TryReport(DownloadProgress p)
        {
            var now = sw.ElapsedTicks;
            var last = Interlocked.Read(ref lastReportTick);
            if (now - last < reportInterval) return;
            if (Interlocked.CompareExchange(ref lastReportTick, now, last) != last) return;
            progress?.Report(p);
        }

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = ct },
            async (file, fileCt) =>
            {
                var url = string.IsNullOrEmpty(file.PackageFolder)
                    ? BaseUrl + file.Path
                    : BaseUrl + file.PackageFolder + "/" + file.Path;
                var destPath = Path.Combine(gameDirectory, file.Path.Replace('/', Path.DirectorySeparatorChar));
                var dir = Path.GetDirectoryName(destPath);
                if (dir != null)
                    Directory.CreateDirectory(dir);

                Exception? lastEx = null;
                for (int attempt = 1; attempt <= MaxFileAttempts; attempt++)
                {
                    long fileBytesThisAttempt = 0;
                    try
                    {
                        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, fileCt);
                        response.EnsureSuccessStatusCode();

                        await using var networkStream = await response.Content.ReadAsStreamAsync(fileCt);
                        await using var fileStream = new FileStream(
                            destPath, FileMode.Create, FileAccess.Write, FileShare.None, ChunkSize, useAsync: true);

                        var buffer = new byte[ChunkSize];
                        int read;
                        while ((read = await networkStream.ReadAsync(buffer, fileCt)) > 0)
                        {
                            await fileStream.WriteAsync(buffer.AsMemory(0, read), fileCt);
                            fileBytesThisAttempt += read;
                            var downloaded = Interlocked.Add(ref bytesDownloaded, read);
                            AddSample(read);

                            TryReport(new DownloadProgress(
                                BytesDownloaded: downloaded,
                                TotalBytes: totalBytes,
                                SpeedBytesPerSec: GetSpeedBytesPerSec(),
                                CurrentFile: file.Path,
                                FilesDownloaded: Volatile.Read(ref filesDownloaded),
                                TotalFiles: files.Count));
                        }

                        lastEx = null;
                        break; // success
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        lastEx = ex;
                        // Roll back partial bytes so the progress bar stays accurate
                        if (fileBytesThisAttempt > 0)
                            Interlocked.Add(ref bytesDownloaded, -fileBytesThisAttempt);

                        if (attempt < MaxFileAttempts)
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), fileCt);
                    }
                }

                if (lastEx != null)
                    throw lastEx;

                Interlocked.Increment(ref filesDownloaded);
            });

        // Final report — guaranteed after all files complete
        progress?.Report(new DownloadProgress(
            BytesDownloaded: bytesDownloaded,
            TotalBytes: totalBytes,
            SpeedBytesPerSec: 0,
            CurrentFile: "",
            FilesDownloaded: filesDownloaded,
            TotalFiles: files.Count));
    }
}
