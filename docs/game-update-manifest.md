# Game Update Manifest System

## Overview

The Dota 2 Classic backend serves a manifest file that describes every expected game file. The launcher uses this manifest to determine which files are missing or outdated and need to be downloaded.

Manifest URL: `https://launcher.dotaclassic.ru/files/manifest.json`

---

## Manifest Format

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

| Field | Type | Description |
|-------|------|-------------|
| `path` | string | Relative path within the game directory, forward-slash separated |
| `hash` | string | MD5 hex digest (lowercase) of the file's content |
| `size` | number | File size in bytes |
| `mode` | string | Sync mode: `"exact"` or `"existing"` |

### Sync Modes

- **`exact`** — The file must exist AND its MD5 hash must match. Used for binaries, assets, and any file the server wants to control precisely. If the file is missing or its hash differs, it must be re-downloaded.
- **`existing`** — The file only needs to exist; its content is not verified. Used for user-editable files such as configs and settings that the player may have modified locally. If the file is absent it will be downloaded (providing a default), but local modifications are preserved.

---

## C# Model

```csharp
// Models/GameManifest.cs
public enum ManifestFileMode { Exact, Existing }

public class GameManifestFile
{
    public string Path { get; set; }   // relative path, forward slashes
    public string Hash { get; set; }   // lowercase MD5 hex
    public long Size { get; set; }
    public string Mode { get; set; }   // "exact" | "existing"

    [JsonIgnore]
    public ManifestFileMode FileMode { get; }  // parsed from Mode
}

public class GameManifest
{
    public List<GameManifestFile> Files { get; set; }
}
```

---

## LocalManifestService

**File:** `Services/LocalManifestService.cs`
**Interface:** `Services/ILocalManifestService.cs`

Scans the game directory and produces a `GameManifest` representing what is currently installed on disk.

```csharp
Task<GameManifest> BuildAsync(
    string gameDirectory,
    IProgress<(int done, int total)>? progress = null,
    CancellationToken ct = default);
```

### How it works

1. Enumerates all files recursively with `DirectoryInfo.GetFiles("*", SearchOption.AllDirectories)` (~30 000 files for a full install).
2. For each file:
   - Computes MD5 hash via `System.Security.Cryptography.MD5`; the computation is offloaded to the thread pool with `Task.Run` to keep the UI thread responsive.
   - Records `FileInfo.Length` as the size.
   - Normalizes the path to forward slashes with `Path.GetRelativePath` + `Replace('\\', '/')`.
   - All local files are recorded with `mode = "exact"` since the local manifest just describes what exists.
3. Reports `(done, total)` progress after each file.

### Performance note

Hashing 30 000 files is IO-bound. The service does not parallelize across files (to avoid overwhelming the disk), but each individual hash computation runs off the UI thread. A full scan of a typical installation takes roughly 30–120 seconds depending on disk speed.

---

## ManifestDiffService

**File:** `Services/ManifestDiffService.cs`
**Interface:** `Services/IManifestDiffService.cs`

Compares a remote manifest against a local manifest and returns the list of files that need to be downloaded.

```csharp
IReadOnlyList<GameManifestFile> ComputeFilesToDownload(
    GameManifest remote,
    GameManifest local);
```

### Algorithm

```
localIndex = Dictionary(path → file, case-insensitive)  // Windows FS is case-insensitive

for each remoteFile in remote.Files:
    localFile = localIndex[remoteFile.Path]  // OrdinalIgnoreCase lookup

    if localFile is missing:
        → download  (both modes)

    if remoteFile.mode == "exact" AND localFile.Hash != remoteFile.Hash:
        → download  (content mismatch)

    // "existing" + file present → skip regardless of hash
```

The diff is synchronous. For 30 000 files the dictionary construction and lookup takes under 5 ms and does not need to be offloaded.

### Path comparison

Paths are compared case-insensitively (`StringComparer.OrdinalIgnoreCase`) because Windows NTFS is case-insensitive — a local scan might yield `dota/Cfg/autoexec.cfg` while the manifest lists `dota/cfg/autoexec.cfg`.

### Hash comparison

Hashes are compared case-insensitively (`StringComparison.OrdinalIgnoreCase`). The remote manifest uses lowercase hex; the local manifest also uses lowercase, but this guards against any inconsistency.

---

## Intended Update Flow

```
1. Fetch remote manifest  (HTTP GET manifest.json)
2. Build local manifest   (LocalManifestService.BuildAsync)
3. Compute diff           (ManifestDiffService.ComputeFilesToDownload)
4. Download missing files (not yet implemented)
5. Verify after download  (optional: rebuild local manifest and diff again)
```

Steps 1–3 are implemented. Step 4 (actual file download) is a future task.
