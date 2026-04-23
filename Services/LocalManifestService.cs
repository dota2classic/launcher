using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public class LocalManifestService : ILocalManifestService
{
    public async Task<GameManifest> BuildAsync(
        string gameDirectory,
        IProgress<(int done, int total)>? progress = null,
        CancellationToken ct = default,
        ManifestScanOptions? options = null)
    {
        options ??= ManifestScanOptions.Foreground;
        var root = new DirectoryInfo(gameDirectory);
        var allFiles = root.GetFiles("*", SearchOption.AllDirectories);
        var files = new GameManifestFile[allFiles.Length];
        var done = 0;

        // Load the hash cache - lets us skip re-hashing files whose size and mtime
        // haven't changed since the last scan (critical on HDD where reads are slow).
        var cache = LocalManifestCache.Load();
        var cacheUpdates = new ConcurrentDictionary<string, LocalManifestCacheEntry>(StringComparer.OrdinalIgnoreCase);

        // Use sequential I/O for HDD (avoids seek-head thrashing) and parallel for SSD.
        var parallelism = options.MaxDegreeOfParallelism ?? GetOptimalParallelism(gameDirectory);
        var driveLabel = options.Throttled
            ? $"background throttled (parallel x{parallelism})"
            : parallelism == 1
                ? "HDD/unknown (sequential)"
                : $"SSD (parallel x{parallelism})";
        AppLog.Info($"[Scan] {allFiles.Length} files, cache entries: {cache.Count}, drive: {driveLabel}");

        await Parallel.ForEachAsync(
            Enumerate(allFiles),
            new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = ct },
            (item, _) =>
            {
                var (file, index) = item;
                var relativePath = Path.GetRelativePath(gameDirectory, file.FullName)
                    .Replace('\\', '/');

                string hash;
                if (cache.TryGetValue(relativePath, out var entry) &&
                    entry.Size == file.Length &&
                    entry.LastWriteUtcTicks == file.LastWriteTimeUtc.Ticks)
                {
                    // Cache hit - file is unchanged, reuse stored hash without reading the file.
                    hash = entry.Hash;
                }
                else
                {
                    // Cache miss - hash the file and record the result for next time.
                    hash = ComputeMd5(file.FullName);
                    cacheUpdates[relativePath] = new LocalManifestCacheEntry(
                        file.Length, file.LastWriteTimeUtc.Ticks, hash);
                }

                files[index] = new GameManifestFile
                {
                    Path = relativePath,
                    Hash = hash,
                    Size = file.Length,
                    Mode = "exact",
                };

                var d = Interlocked.Increment(ref done);
                progress?.Report((d, allFiles.Length));
                if (options.Throttled &&
                    options.BatchDelay > TimeSpan.Zero &&
                    options.BatchSize > 0 &&
                    d % options.BatchSize == 0)
                {
                    return new ValueTask(Task.Delay(options.BatchDelay, ct));
                }

                return ValueTask.CompletedTask;
            });

        if (!cacheUpdates.IsEmpty)
            LocalManifestCache.Save(cache, cacheUpdates);

        return new GameManifest { Files = [..files] };
    }

    private static IEnumerable<(FileInfo, int)> Enumerate(FileInfo[] files)
    {
        for (var i = 0; i < files.Length; i++)
            yield return (files[i], i);
    }

    private static string ComputeMd5(string filePath)
    {
        using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 131072);
        using var md5 = MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// Returns the ideal <see cref="ParallelOptions.MaxDegreeOfParallelism"/> for reading
    /// files under <paramref name="gameDirectory"/>.
    /// Detects whether the drive is an SSD via WMI:
    ///   First try the direct MSFT_PhysicalDisk.DeviceId == Win32_DiskDrive.Index mapping
    ///   (works on the common single-node provider used by most desktops).
    ///   If that fails, fall back to MSFT_StorageNodeToPhysicalDisk.DiskNumber and
    ///   the associated MSFT_PhysicalDisk.MediaType.
    ///   4 = SSD -> parallel reads (CPU-bound MD5 benefits from concurrency)
    ///   3 = HDD or unknown -> 1 (sequential, avoids disk-head thrashing)
    /// Falls back to 1 on any WMI failure so HDD users are always protected.
    /// </summary>
    private static int GetOptimalParallelism(string gameDirectory)
    {
        try
        {
            var pathRoot = Path.GetPathRoot(gameDirectory);
            if (pathRoot == null) return 1;

            var driveLetter = pathRoot.TrimEnd('\\', '/'); // e.g. "C:"

            // Step 1: logical disk -> disk partition
            using var lpSearcher = new ManagementObjectSearcher(
                "root\\CIMV2",
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}'}} " +
                "WHERE AssocClass=Win32_LogicalDiskToPartition");

            foreach (ManagementObject partition in lpSearcher.Get())
            {
                var partId = partition["DeviceID"]?.ToString();
                if (partId == null) continue;

                // Step 2: disk partition -> physical disk drive
                using var dpSearcher = new ManagementObjectSearcher(
                    "root\\CIMV2",
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partId}'}} " +
                    "WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                foreach (ManagementObject disk in dpSearcher.Get())
                {
                    if (!TryGetDiskIndex(disk, out var diskNumber))
                        continue;

                    if (!TryGetPhysicalDiskMediaType(diskNumber, out var mediaType))
                        continue;

                    // MediaType 4 = SSD; anything else (3=HDD, 0=Unspecified) -> sequential.
                    return mediaType == 4
                        ? Math.Min(Environment.ProcessorCount, 8)
                        : 1;
                }
            }
        }
        catch
        {
            // WMI unavailable or failed -> fall through to the safe default.
        }

        return 1; // Sequential: safe for HDD, acceptable for SSD
    }

    private static bool TryGetDiskIndex(ManagementObject disk, out int diskIndex)
    {
        if (disk["Index"] != null)
        {
            diskIndex = Convert.ToInt32(disk["Index"]);
            return true;
        }

        // Older APIs expose the physical drive number only via DeviceID ("\\.\PHYSICALDRIVE2").
        var deviceId = disk["DeviceID"]?.ToString() ?? "";
        var digits = new string(deviceId.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out diskIndex);
    }

    private static bool TryGetPhysicalDiskMediaType(int diskNumber, out int mediaType)
    {
        mediaType = 0;

        var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
        scope.Connect();

        if (TryGetPhysicalDiskMediaTypeByDeviceId(scope, diskNumber, out mediaType))
            return true;

        using var relationSearcher = new ManagementObjectSearcher(
            scope,
            new ObjectQuery(
                $"SELECT DiskNumber, PhysicalDisk FROM MSFT_StorageNodeToPhysicalDisk WHERE DiskNumber = {diskNumber}"));

        foreach (ManagementObject relation in relationSearcher.Get())
        {
            var physicalDiskPath = relation["PhysicalDisk"]?.ToString();
            if (string.IsNullOrWhiteSpace(physicalDiskPath))
                continue;

            using var physicalDisk = new ManagementObject(scope, new ManagementPath(physicalDiskPath), null);
            physicalDisk.Get();

            mediaType = physicalDisk["MediaType"] != null
                ? Convert.ToInt32(physicalDisk["MediaType"])
                : 0;

            return true;
        }

        return false;
    }

    private static bool TryGetPhysicalDiskMediaTypeByDeviceId(
        ManagementScope scope,
        int diskNumber,
        out int mediaType)
    {
        mediaType = 0;

        using var physicalDiskSearcher = new ManagementObjectSearcher(
            scope,
            new ObjectQuery(
                $"SELECT DeviceId, MediaType FROM MSFT_PhysicalDisk WHERE DeviceId = '{diskNumber}'"));

        foreach (ManagementObject physicalDisk in physicalDiskSearcher.Get())
        {
            mediaType = physicalDisk["MediaType"] != null
                ? Convert.ToInt32(physicalDisk["MediaType"])
                : 0;

            return true;
        }

        return false;
    }
}
