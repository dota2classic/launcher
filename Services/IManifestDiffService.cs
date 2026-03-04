using System.Collections.Generic;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public interface IManifestDiffService
{
    IReadOnlyList<GameManifestFile> ComputeFilesToDownload(
        GameManifest remote,
        GameManifest local);
}
