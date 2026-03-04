using System;
using System.Collections.Generic;
using System.Linq;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public class ManifestDiffService : IManifestDiffService
{
    public IReadOnlyList<GameManifestFile> ComputeFilesToDownload(
        GameManifest remote,
        GameManifest local)
    {
        var localByPath = local.Files.ToDictionary(
            f => f.Path,
            StringComparer.OrdinalIgnoreCase);

        var toDownload = new List<GameManifestFile>();

        foreach (var remoteFile in remote.Files)
        {
            if (!localByPath.TryGetValue(remoteFile.Path, out var localFile))
            {
                toDownload.Add(remoteFile);
                continue;
            }

            if (remoteFile.FileMode == ManifestFileMode.Exact &&
                !string.Equals(localFile.Hash, remoteFile.Hash, StringComparison.OrdinalIgnoreCase))
            {
                toDownload.Add(remoteFile);
            }
            // Existing mode: file is present — no action needed regardless of hash
        }

        return toDownload;
    }
}
