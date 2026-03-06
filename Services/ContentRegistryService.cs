using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public class ContentRegistryService : IContentRegistryService
{
    private const string RegistryUrl = "https://launcher.dotaclassic.ru/files/registry.json";
    private readonly HttpClient _http = new();
    private ContentRegistry? _cached;

    public async Task<ContentRegistry?> GetAsync()
    {
        if (_cached != null)
            return _cached;

        try
        {
            AppLog.Info($"[ContentRegistry] Fetching registry from {RegistryUrl}");
            var json = await _http.GetStringAsync(RegistryUrl);
            AppLog.Info($"[ContentRegistry] Received {json.Length} chars");
            _cached = JsonSerializer.Deserialize<ContentRegistry>(json);
            AppLog.Info($"[ContentRegistry] Parsed {_cached?.Packages?.Count ?? 0} package(s)");
            return _cached;
        }
        catch (Exception ex)
        {
            AppLog.Error($"[ContentRegistry] Failed to fetch registry: {ex.Message}", ex);
            return null;
        }
    }

    public void Invalidate() => _cached = null;
}
