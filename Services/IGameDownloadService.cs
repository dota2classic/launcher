using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public interface IGameDownloadService
{
    Task DownloadFilesAsync(
        IReadOnlyList<GameManifestFile> files,
        string gameDirectory,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default);
}
