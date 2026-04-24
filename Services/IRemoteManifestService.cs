using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public sealed class RemotePackageManifest
{
    public required ContentPackage Package { get; init; }
    public required GameManifest Manifest { get; init; }
}

public sealed class RemoteManifestSet
{
    public IReadOnlyList<RemotePackageManifest> Packages { get; init; } = [];
    public GameManifest CombinedManifest { get; init; } = new();
    public IReadOnlyList<string> InstalledPackageIds { get; init; } = [];
}

public interface IRemoteManifestService
{
    Task<RemoteManifestSet> GetInstalledPackageManifestsAsync(
        IReadOnlyCollection<string>? selectedDlcIds,
        bool includeOptionalPackages = false,
        bool forceRefreshRegistry = false,
        CancellationToken cancellationToken = default);
}
