using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public sealed class RemoteManifestService : IRemoteManifestService, IDisposable
{
    private const string BaseManifestUrl = "https://launcher.dotaclassic.ru/files/";
    private static readonly Regex SafeFolderName = new(@"^[a-zA-Z0-9_\-]+$", RegexOptions.Compiled);

    private readonly IContentRegistryService _registryService;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

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

        var tasks = packagesToInstall.Select(pkg => FetchPackageManifestAsync(pkg, cancellationToken));
        var packageManifests = (await Task.WhenAll(tasks)).ToList();

        return new RemoteManifestSet
        {
            Packages = packageManifests,
            CombinedManifest = Combine(packageManifests.SelectMany(p => p.Manifest.Files)),
            InstalledPackageIds = packagesToInstall.Select(p => p.Id).ToList(),
        };
    }

    private async Task<RemotePackageManifest> FetchPackageManifestAsync(ContentPackage pkg, CancellationToken cancellationToken)
    {
        if (!SafeFolderName.IsMatch(pkg.Folder))
            throw new Exception($"Invalid package folder name: {pkg.Folder}");

        var manifestUrl = $"{BaseManifestUrl}{pkg.Folder}/manifest.json";
        var json = await _httpClient.GetStringAsync(manifestUrl, cancellationToken);
        var manifest = JsonSerializer.Deserialize<GameManifest>(json)
            ?? throw new Exception(I18n.T("game.emptyPackageManifest", ("name", pkg.Name)));

        foreach (var file in manifest.Files)
        {
            if (Path.IsPathRooted(file.Path) || file.Path.Contains(".."))
                throw new Exception($"Unsafe path in manifest for package {pkg.Name}: {file.Path}");

            file.PackageFolder = pkg.Folder;
            file.PackageName = pkg.Name;
        }

        return new RemotePackageManifest { Package = pkg, Manifest = manifest };
    }

    private static GameManifest Combine(IEnumerable<GameManifestFile> files)
    {
        // Later packages win when two manifests address the same path.
        var byPath = new Dictionary<string, GameManifestFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
            byPath[file.Path] = file;
        return new GameManifest { Files = byPath.Values.ToList() };
    }

    public void Dispose() => _httpClient.Dispose();
}
