using System.Threading;
using System.Threading.Tasks;

namespace d2c_launcher.Services;

public interface IHttpImageService
{
    Task<byte[]?> LoadBytesAsync(string? url, CancellationToken cancellationToken = default);
}
