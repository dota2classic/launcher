using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using d2c_launcher.Integration;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public sealed class EmoticonService : IEmoticonService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IBackendApiService _backendApiService;
    private readonly IHttpImageService _httpImageService;
    private readonly ISteamManager _steamManager;
    private readonly string _cacheDir;

    public EmoticonService(IBackendApiService backendApiService, IHttpImageService httpImageService, ISteamManager steamManager)
    {
        _backendApiService = backendApiService;
        _httpImageService = httpImageService;
        _steamManager = steamManager;
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "d2c-launcher", "emoticons");
    }

    public async Task<EmoticonLoadResult> LoadEmoticonsAsync()
    {
        Directory.CreateDirectory(_cacheDir);

        var steamId = _steamManager.CurrentUser?.SteamId32.ToString();
        var list = await _backendApiService.GetEmoticonsAsync(steamId).ConfigureAwait(false);
        var validCodes = new HashSet<string>(list.Select(e => e.Code), StringComparer.Ordinal);

        // Clean up cache files for emoticons that no longer exist.
        CleanStaleEntries(validCodes);

        var tasks = list.Select(async e =>
        {
            var bytes = await LoadWithCacheAsync(e.Code, e.Src).ConfigureAwait(false);
            return (e.Code, bytes);
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var images = new Dictionary<string, byte[]>(results.Length, StringComparer.Ordinal);
        foreach (var (code, bytes) in results)
        {
            if (bytes != null)
                images[code] = bytes;
        }

        AppLog.Info($"Emoticons: loaded {images.Count} images ({list.Count} in list).");
        return new EmoticonLoadResult { Images = images, Ordered = list };
    }

    private async Task<byte[]?> LoadWithCacheAsync(string code, string url)
    {
        var dataPath = Path.Combine(_cacheDir, SanitizeFileName(code) + ".gif");
        var metaPath = dataPath + ".meta";

        if (File.Exists(dataPath) && File.Exists(metaPath))
        {
            try
            {
                var meta = JsonSerializer.Deserialize<EmoticonMeta>(await File.ReadAllTextAsync(metaPath).ConfigureAwait(false));
                if (meta != null && DateTime.UtcNow - meta.CachedAt < Ttl)
                    return await File.ReadAllBytesAsync(dataPath).ConfigureAwait(false);
            }
            catch
            {
                // Corrupt meta — re-download below.
            }
        }

        var bytes = await _httpImageService.LoadBytesAsync(url).ConfigureAwait(false);
        if (bytes == null)
            return null;

        try
        {
            await File.WriteAllBytesAsync(dataPath, bytes).ConfigureAwait(false);
            var meta = JsonSerializer.Serialize(new EmoticonMeta { CachedAt = DateTime.UtcNow });
            await File.WriteAllTextAsync(metaPath, meta).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLog.Error($"Emoticons: failed to write cache for '{code}': {ex.Message}", ex);
        }

        return bytes;
    }

    private void CleanStaleEntries(HashSet<string> validCodes)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_cacheDir, "*.gif"))
            {
                var code = Path.GetFileNameWithoutExtension(file);
                if (!validCodes.Contains(code))
                {
                    File.Delete(file);
                    var meta = file + ".meta";
                    if (File.Exists(meta)) File.Delete(meta);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error($"Emoticons: cache cleanup failed: {ex.Message}", ex);
        }
    }

    private static string SanitizeFileName(string code)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(code.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c));
    }

    private sealed class EmoticonMeta
    {
        public DateTime CachedAt { get; set; }
    }
}
