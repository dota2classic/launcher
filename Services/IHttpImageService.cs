using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace d2c_launcher.Services;

public interface IHttpImageService
{
    Task<Bitmap?> LoadBitmapAsync(string? url, CancellationToken cancellationToken = default);
    Task<byte[]?> LoadBytesAsync(string? url, CancellationToken cancellationToken = default);
}
