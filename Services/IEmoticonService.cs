using System.Collections.Generic;
using System.Threading.Tasks;

namespace d2c_launcher.Services;

public interface IEmoticonService
{
    /// <summary>Returns a map of emoticon code → GIF bytes, using disk cache with TTL.</summary>
    Task<Dictionary<string, byte[]>> GetEmoticonImagesAsync();
}
