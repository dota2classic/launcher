# Game Update Manifest System

## Overview

The CDN serves a **registry** of content packages. Each package has its own manifest listing expected files. The launcher fetches the registry, resolves which packages to use (required + user-selected optional), fetches their manifests, scans local files, diffs, and downloads anything missing or outdated.

CDN base URL: `https://launcher.dotaclassic.ru/files/`

---

## Step 1 — Content Registry

**URL:** `GET /files/registry.json`
**Service:** `ContentRegistryService` / `IContentRegistryService` (singleton, in-memory cache, `Invalidate()` to bust)

```json
{
  "packages": [
    { "id": "core",    "folder": "core",    "name": "Dota 2 Classic", "optional": false },
    { "id": "maps_ru", "folder": "maps_ru", "name": "Русские карты",  "optional": true  }
  ]
}
```

**C# model** (`Models/ContentRegistry.cs`):
```csharp
public class ContentPackage
{
    public string Id     { get; set; }   // unique identifier
    public string Folder { get; set; }   // CDN subfolder; used to build URLs
    public string Name   { get; set; }   // display name (Russian)
    public bool   Optional { get; set; } // false = always downloaded
}

public class ContentRegistry
{
    public List<ContentPackage> Packages { get; set; }
}
```

**Package selection:**
- Required packages (`Optional == false`) are always included.
- Optional packages are selected by the user; choices persisted in `LauncherSettings.SelectedDlcIds`.
- Selection UI shown on first install (after folder pick in `SelectGameView`) and in the Launcher settings tab.

---

## Step 2 — Per-Package Manifest

**URL:** `GET /files/{package.Folder}/manifest.json`

One manifest per package. Format is identical across packages:

```json
{
  "files": [
    {
      "path": "_CommonRedist/DirectX/Jun2010/APR2007_XACT_x64.cab",
      "hash": "8acbb49a7c2a97c12f63c16bdd8f7512",
      "size": 195766,
      "mode": "exact"
    },
    {
      "path": "dota/cfg/autoexec.cfg",
      "hash": "d41d8cd98f00b204e9800998ecf8427e",
      "size": 0,
      "mode": "existing"
    }
  ]
}
```

| Field  | Type   | Description |
|--------|--------|-------------|
| `path` | string | Relative path within the game directory, forward-slash separated |
| `hash` | string | MD5 hex digest (lowercase) |
| `size` | number | File size in bytes |
| `mode` | string | Sync mode: `"exact"` or `"existing"` |

### Sync Modes

- **`exact`** — File must exist and MD5 must match. Used for binaries and assets.
- **`existing`** — File only needs to exist; content is not verified. Used for user-editable configs — if absent, the default is downloaded; local changes are preserved.

**C# model** (`Models/GameManifest.cs`):
```csharp
public class GameManifestFile
{
    public string Path   { get; set; }
    public string Hash   { get; set; }
    public long   Size   { get; set; }
    public string Mode   { get; set; }

    [JsonIgnore] public ManifestFileMode FileMode { get; }  // parsed from Mode
    [JsonIgnore] public string PackageFolder { get; set; }  // set at runtime from ContentPackage.Folder
    [JsonIgnore] public string PackageName   { get; set; }  // set at runtime from ContentPackage.Name
}
```

`PackageFolder` is set when loading from the registry so the download service can construct the correct URL without needing to look up the package again.

---

## Step 3 — Local Manifest Scan

**Service:** `LocalManifestService` / `ILocalManifestService`

Scans the game directory and produces a `GameManifest` of installed files.

```csharp
Task<GameManifest> BuildAsync(
    string gameDirectory,
    IProgress<(int done, int total)>? progress = null,
    CancellationToken ct = default);
```

### Hash cache

Hashing ~30 000 files is slow, especially on HDD. `LocalManifestCache` persists `(size, mtime_ticks, MD5)` per file to:

```
%LocalAppData%\d2c-launcher\local_manifest_cache.json
```

On each scan, if a file's `Length` and `LastWriteTimeUtc.Ticks` match the cached entry, the stored hash is reused without reading the file. Only changed/new files are re-hashed. Failures are silently swallowed — the cache is purely advisory.

### Drive detection and parallelism

`GetOptimalParallelism()` uses WMI to detect whether the game directory lives on an SSD or HDD:

1. `Win32_LogicalDisk` → `Win32_DiskPartition` (via `Win32_LogicalDiskToPartition`)
2. `Win32_DiskPartition` → `Win32_DiskDrive` (via `Win32_DiskDriveToDiskPartition`)
3. `MSFT_PhysicalDisk.MediaType` where `Number` matches the physical drive index

| `MediaType` | Meaning | Parallelism |
|-------------|---------|-------------|
| 4 | SSD | `min(CPU count, 8)` |
| 3 / 0 / error | HDD or unknown | 1 (sequential) |

Sequential I/O avoids disk-head thrashing on HDD. Falls back to 1 on any WMI failure.

---

## Step 4 — Diff

**Service:** `ManifestDiffService` / `IManifestDiffService`

```csharp
IReadOnlyList<GameManifestFile> ComputeFilesToDownload(
    GameManifest remote,
    GameManifest local);
```

Algorithm (synchronous, <5 ms for 30 000 files):

```
localIndex = Dictionary(path → file, OrdinalIgnoreCase)

for each remoteFile in remote.Files:
    localFile = localIndex[remoteFile.Path]

    if localFile is missing:
        → download  (both modes)

    if remoteFile.mode == "exact" AND localFile.Hash != remoteFile.Hash (OrdinalIgnoreCase):
        → download  (content mismatch)

    // "existing" + file present → skip regardless of hash
```

Paths and hashes are compared case-insensitively to match Windows NTFS semantics.

---

## Step 5 — Download

**Service:** `GameDownloadService` / `IGameDownloadService`

Download URL per file: `GET /files/{PackageFolder}/{file.Path}`

- Streams in 256 KB chunks directly to the target path.
- Reports rolling speed (bytes/s), ETA, and current filename via `DownloadProgress`.
- `GameDownloadViewModel` orchestrates all phases and drives the `GameDownloadView` progress UI.

---

## Full Update Flow

```
1. GET /files/registry.json              → ContentRegistry
2. Filter packages by SelectedDlcIds
3. GET /files/{folder}/manifest.json     → one GameManifest per package
4. Merge all package manifests into one combined remote GameManifest
5. LocalManifestService.BuildAsync()     → local GameManifest (with hash cache + SSD/HDD parallelism)
6. ManifestDiffService.ComputeFilesToDownload(remote, local)
7. GameDownloadService.DownloadAsync()   → streams missing/changed files from CDN
8. (Optional) Re-scan to verify         → not currently done post-download
```

This flow runs every launch in `GameDownloadView`, between `SelectGameView` and `MainLauncherView`.
