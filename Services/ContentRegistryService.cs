using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using d2c_launcher.Models;

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
            var json = await _http.GetStringAsync(RegistryUrl);
            _cached = JsonSerializer.Deserialize<ContentRegistry>(json);
            return _cached;
        }
        catch
        {
            return null;
        }
    }

    public void Invalidate() => _cached = null;
}
