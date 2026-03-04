# Active Context

## Current Focus

**Game download / verification flow** — new `GameDownloadView` that runs on every launch between selecting the game folder and showing the main UI.

### What Was Built
- `Models/DownloadProgress.cs` — progress record (bytes, speed, ETA, file count)
- `Services/IGameDownloadService.cs` + `GameDownloadService.cs` — HTTP download with 256 KB streaming, rolling speed window
- `ViewModels/GameDownloadViewModel.cs` — phases: fetching manifest → scanning local → downloading → complete/error
- `Views/GameDownloadView.axaml` — progress bar, status text, speed/ETA, current file
- `Views/SelectGameView.axaml` — redesigned with "Скачать игру" (primary) + "У меня уже установлена дота" (secondary text link); both open folder picker
- `MainWindowViewModel` — now always routes through `GameDownloadView` before `MainLauncherView` when game dir is set; removed old background `RunManifestDiffAsync`
- Download URL: `https://launcher.dotaclassic.ru/files/{path}`
- Added `GameDownload` preview entry to `PreviewRegistry`

### New State Machine
```
Steam not running → LaunchSteamFirstView
Game dir not set → SelectGameView (folder picker)
Game dir set → GameDownloadView (always: fetch manifest, scan local, diff, download)
Download complete → MainLauncherView
```

---

## Previous Work: HardwareInfoService

### What It Does
- Queries WMI classes: `Win32_Processor`, `Win32_BaseBoard`, `Win32_DiskDrive`, `Win32_NetworkAdapterConfiguration`, `Win32_PhysicalMemory`, `Win32_VideoController`, `Win32_OperatingSystem`
- Computes a SHA-256 **HWID** from CPU ID + mobo serial + disk serials + MAC addresses
- Logs everything via `AppLog.Info()` tagged with `[HW]`
- Static class (`HardwareInfoService.LogAll()`) — no DI needed

### Files Changed (uncommitted)
| File | Change |
|------|--------|
| `Services/HardwareInfoService.cs` | New — WMI queries + HWID computation |
| `Program.cs` | Modified — likely calls `HardwareInfoService.LogAll()` at startup |
| `d2c-launcher.csproj` | Modified — adds `System.Management` NuGet reference |

### What's Still Needed
- Verify `Program.cs` wires up `HardwareInfoService.LogAll()` at startup
- Consider sending HWID to backend API (for analytics or ban enforcement)

---

## Developer Tools Added

- `tools/screenshot-html.ps1` — renders any HTML file via headless Chrome and saves a PNG to `tools/screenshots/`. Args: `HtmlFile` (required), `-Width` (default 1000), `-Height` (default 800). No build step, no Steam needed.

---

## Recent Completed Work (from git log)

| Commit | Change |
|--------|--------|
| `dc61ce4` | Add test step to CI workflow |
| `212c9ca` | Remove .claude from git, add to .gitignore |
| `e1f4024` | Add xUnit test project (CfgGenerator, DotaCfgReader, CvarMappings) |
| `b26db9b` | Bi-directional config sync with `host_writeconfig` flush + colorblind setting |
| `48de235` | Language + NoVid launch settings in settings modal |

---

## Decisions Made Recently

- **Two-phase sync:** `host_writeconfig` → 1.5s delay → read config.cfg. Required because Source engine's `WM_COPYDATA` handler queues commands asynchronously.
- **Static HardwareInfoService:** No interface/DI needed — it's a diagnostic utility called once at startup.
- **`System.Management`:** Added as NuGet package for WMI access; Windows-only, which is fine (this app is Windows-only).
