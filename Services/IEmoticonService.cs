using System.Collections.Generic;
using System.Threading.Tasks;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public sealed class EmoticonLoadResult
{
    public Dictionary<string, byte[]> Images { get; init; } = new();
    public IReadOnlyList<EmoticonData> Ordered { get; init; } = System.Array.Empty<EmoticonData>();
}

public interface IEmoticonService
{
    /// <summary>Returns GIF bytes by code and the backend-ordered list with IDs.</summary>
    Task<EmoticonLoadResult> LoadEmoticonsAsync(string? steamId = null);
}
