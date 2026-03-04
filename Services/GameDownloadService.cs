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

        var bytesDownloaded = 0L;
        var filesDownloaded = 0;

        // Rolling speed window: list of (timestamp, bytes) samples
        var speedSamples = new Queue<(long Ticks, long Bytes)>();
        var speedWindowBytes = 0L;
        var sw = Stopwatch.StartNew();

        void AddSample(long bytes)
        {
            var now = sw.ElapsedTicks;
            speedSamples.Enqueue((now, bytes));
            speedWindowBytes += bytes;

            // Evict old samples outside the window
            var cutoff = now - (long)(SpeedWindow.TotalSeconds * Stopwatch.Frequency);
            while (speedSamples.Count > 0 && speedSamples.Peek().Ticks < cutoff)
                speedWindowBytes -= speedSamples.Dequeue().Bytes;
        }

        double GetSpeedBytesPerSec()
        {
            if (speedSamples.Count < 2) return 0;
            var oldest = speedSamples.Peek().Ticks;
            var elapsed = (sw.ElapsedTicks - oldest) / (double)Stopwatch.Frequency;
            return elapsed > 0 ? speedWindowBytes / elapsed : 0;
        }

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            var url = BaseUrl + file.Path;
            var destPath = Path.Combine(gameDirectory, file.Path.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(destPath);
            if (dir != null)
                Directory.CreateDirectory(dir);

            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var networkStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(
                destPath, FileMode.Create, FileAccess.Write, FileShare.None, ChunkSize, useAsync: true);

            var buffer = new byte[ChunkSize];
            int read;
            while ((read = await networkStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                bytesDownloaded += read;
                AddSample(read);

                progress?.Report(new DownloadProgress(
                    BytesDownloaded: bytesDownloaded,
                    TotalBytes: totalBytes,
                    SpeedBytesPerSec: GetSpeedBytesPerSec(),
                    CurrentFile: file.Path,
                    FilesDownloaded: filesDownloaded,
                    TotalFiles: files.Count));
            }

            filesDownloaded++;
            progress?.Report(new DownloadProgress(
                BytesDownloaded: bytesDownloaded,
                TotalBytes: totalBytes,
                SpeedBytesPerSec: GetSpeedBytesPerSec(),
                CurrentFile: file.Path,
                FilesDownloaded: filesDownloaded,
                TotalFiles: files.Count));
        }
    }
}
