using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public sealed class RemoteManifestService : IRemoteManifestService, IDisposable
{
    private const string BaseManifestUrl = "https://launcher.dotaclassic.ru/files/";

    private readonly IContentRegistryService _registryService;
    private readonly HttpClient _httpClient = new();

    public RemoteManifestService(IContentRegistryService registryService)
    {
        _registryService = registryService;
    }

    public async Task<RemoteManifestSet> GetInstalledPackageManifestsAsync(
        IReadOnlyCollection<string>? selectedDlcIds,
        bool includeOptionalPackages = false,
        bool forceRefreshRegistry = false,
        CancellationToken cancellationToken = default)
    {
        if (forceRefreshRegistry)
            _registryService.Invalidate();

        var registry = await _registryService.GetAsync();
        if (registry == null || registry.Packages.Count == 0)
            throw new Exception(Resources.Strings.FailedToLoadPackages);

        var selectedIds = (selectedDlcIds ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var packagesToInstall = registry.Packages
            .Where(p => !p.Optional || includeOptionalPackages || selectedIds.Contains(p.Id))
            .ToList();

        var packageManifests = new List<RemotePackageManifest>(packagesToInstall.Count);

        foreach (var pkg in packagesToInstall)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var manifestUrl = $"{BaseManifestUrl}{pkg.Folder}/manifest.json";
            var json = await _httpClient.GetStringAsync(manifestUrl, cancellationToken);
            var manifest = JsonSerializer.Deserialize<GameManifest>(json)
                ?? throw new Exception($"Манифест пакета {pkg.Name} пустой.");

            foreach (var file in manifest.Files)
            {
                file.PackageFolder = pkg.Folder;
                file.PackageName = pkg.Name;
            }

            packageManifests.Add(new RemotePackageManifest
            {
                Package = pkg,
                Manifest = manifest,
            });
        }

        return new RemoteManifestSet
        {
            Packages = packageManifests,
            CombinedManifest = Combine(packageManifests.SelectMany(p => p.Manifest.Files)),
            InstalledPackageIds = packagesToInstall.Select(p => p.Id).ToList(),
        };
    }

    private static GameManifest Combine(IEnumerable<GameManifestFile> files)
    {
        // Later packages win when two manifests address the same path.
        var byPath = new Dictionary<string, GameManifestFile>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            byPath[file.Path] = new GameManifestFile
            {
                Path = file.Path,
                Hash = file.Hash,
                Size = file.Size,
                Mode = file.Mode,
                PackageFolder = file.PackageFolder,
                PackageName = file.PackageName,
            };
        }

        return new GameManifest { Files = byPath.Values.ToList() };
    }

    public void Dispose() => _httpClient.Dispose();
}
