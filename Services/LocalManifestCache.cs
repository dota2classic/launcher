using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace d2c_launcher.Services;

/// <summary>
/// One entry in the local manifest hash cache.
/// Short JSON property names keep the cache file compact.
/// </summary>
public sealed record LocalManifestCacheEntry(
    [property: JsonPropertyName("s")] long Size,
    [property: JsonPropertyName("t")] long LastWriteUtcTicks,
    [property: JsonPropertyName("h")] string Hash);

/// <summary>
/// Persists per-file (size, mtime, MD5) tuples so that files whose size and
/// last-write timestamp have not changed can skip re-hashing on the next scan.
/// Load/Save failures are silently swallowed — the cache is purely advisory.
/// </summary>
internal static class LocalManifestCache
{
    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "d2c-launcher", "local_manifest_cache.json");

    public static ConcurrentDictionary<string, LocalManifestCacheEntry> Load()
    {
        try
        {
            if (!File.Exists(CachePath))
                return Empty();

            var json = File.ReadAllText(CachePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, LocalManifestCacheEntry>>(json);
            return dict != null
                ? new ConcurrentDictionary<string, LocalManifestCacheEntry>(dict, StringComparer.OrdinalIgnoreCase)
                : Empty();
        }
        catch
        {
            return Empty();
        }
    }

    /// <summary>
    /// Merges <paramref name="updates"/> into <paramref name="existing"/> and writes the result.
    /// </summary>
    public static void Save(
        ConcurrentDictionary<string, LocalManifestCacheEntry> existing,
        ConcurrentDictionary<string, LocalManifestCacheEntry> updates)
    {
        try
        {
            foreach (var (k, v) in updates)
                existing[k] = v;

            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            var json = JsonSerializer.Serialize(new Dictionary<string, LocalManifestCacheEntry>(existing));
            File.WriteAllText(CachePath, json);
        }
        catch { /* non-fatal — next run will just re-hash */ }
    }

    private static ConcurrentDictionary<string, LocalManifestCacheEntry> Empty() =>
        new(StringComparer.OrdinalIgnoreCase);
}
