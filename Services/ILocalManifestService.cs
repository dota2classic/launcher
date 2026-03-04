using System;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public interface ILocalManifestService
{
    Task<GameManifest> BuildAsync(
        string gameDirectory,
        IProgress<(int done, int total)>? progress = null,
        CancellationToken ct = default);
}
