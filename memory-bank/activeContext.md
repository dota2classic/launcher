# Active Context

## Current Focus

**Issue #15: Rich message rendering in chat**

Chat messages now parsed into typed segments and rendered as mixed inline content:

- `Models/RichSegment.cs` — `TextSegment`, `RaritySegment`, `UrlSegment`, `EmoticonSegment`
- `Models/EmoticonData.cs` — `record EmoticonData(string Code, string Src)`
- `Util/RichMessageParser.cs` — rule-based parser (rarity tags → colored, `:code:` → emoticon, URLs → clickable); mirrors React client logic
- `Services/IBackendApiService.cs` / `BackendApiService.cs` — added `GetEmoticonsAsync()` calling `v1/forum/emoticons`
- `ViewModels/ChatViewModel.cs` — `LoadEmoticonsAsync()` at startup (loads list + bitmap per emoticon); passes `_emoticonImages` dict to parser
- `Models/ChatMessageView.cs` — added `IReadOnlyList<RichSegment> RichContent` property
- `Views/Components/RichMessageBlock.cs` — custom `UserControl` wrapping `TextBlock.Inlines`; `Run` for text/rarity, `InlineUIContainer(Image)` for emoticons, `InlineUIContainer(TextBlock)` for URLs (click → `Process.Start`)
- `Views/Components/ChatPanel.axaml` — replaced plain `TextBlock Text="{Binding Content}"` with `<components:RichMessageBlock Segments="{Binding RichContent}"/>`
- `Preview/PreviewStubs.cs` — added stub `GetEmoticonsAsync` returning empty list

---

## Previous Focus

**Issue #14: Fix chat message sending (500 "muted" error)**

The POST body for `v1/forum/thread/message` requires `threadId` as `"forum_<uuid>"` (prefixed), while the GET endpoints use the bare UUID + separate `threadType` param. `BackendApiService.PostChatMessageAsync` was passing the bare UUID, causing a 500 "muted" error. Fixed by prepending `"forum_"` in the DTO construction.

- `Services/BackendApiService.cs` — changed `ThreadId = threadId` to `ThreadId = $"forum_{threadId}"` in `PostChatMessageAsync`

---

## Previous Focus

**Issue #12: Replace chat polling with SSE live updates**

Replaced the 10-second `DispatcherTimer` poll in `ChatViewModel` with a persistent Server-Sent Events (SSE) connection:

- `Services/IBackendApiService.cs` — added `SubscribeChatAsync(threadId, bearerToken, ct)` returning `IAsyncEnumerable<ChatMessageData>`
- `Services/BackendApiService.cs` — implemented SSE reader: opens `GET v1/forum/thread/{id}/forum/sse` with `HttpCompletionOption.ResponseHeadersRead`, reads lines, parses `data:` payload as `ThreadMessageDTO` JSON; dedicated `_sseHttpClient` with `Timeout.InfiniteTimeSpan`
- `ViewModels/ChatViewModel.cs` — removed `DispatcherTimer`; added `StartAsync()` (initial load + SSE loop), `RestartSse()` (reconnect with new token), `RunSseLoopAsync()` with 3s reconnect backoff, `ConsumeIncomingMessage()` for upsert/delete of individual messages
- `ViewModels/MainLauncherViewModel.cs` — changed `Chat.RefreshAsync()` → `Chat.StartAsync()` at init; `Chat.RestartSse()` when backend token changes
- `Preview/PreviewStubs.cs` + `PreviewRegistry.cs` — added no-op `SubscribeChatAsync` stub, updated preview to call `StartAsync()`

---

## Previous Focus

**Issue #11: Implement chat window**

Replaced the "Чат пока не реализован" placeholder in `MainLauncherView.axaml` with a fully functional chat component:

- `Models/ChatMessageData.cs` — plain record DTO from service layer
- `Models/ChatMessageView.cs` — observable UI row (Initials, ShowHeader, AvatarImage)
- `IBackendApiService` / `BackendApiService` — added `GetChatMessagesAsync` + `PostChatMessageAsync`; thread ID `17aa3530-d152-462e-a032-909ae69019ed`, type `Forum`
- `ViewModels/ChatViewModel.cs` — groups messages (same author + <60s gap → no header), polls every 10s, loads avatars in background, sends via `PostChatMessageAsync`
- `Views/Components/ChatPanel.axaml/.cs` — avatar circle + name + time (header rows); text-only (follow-up rows); input + send button; auto-scrolls to bottom
- `MainLauncherViewModel` — added `Chat` child VM; disposed in `Dispose()`
- Preview: `ChatPanel` entry in `PreviewRegistry`, stubs in `PreviewStubs`

---

## Previous Focus

**Issue #10 fix: push scan duration metric to Faro**

In `GameDownloadViewModel.ScanLocalFilesAsync()`, wrapped `_localManifestService.BuildAsync()` with a `Stopwatch` and called `FaroTelemetryService.TrackEvent("scan_completed", ...)` after the scan with:
- `duration_ms` — elapsed milliseconds
- `file_count` — number of files in the local manifest

---

## Previous Focus

**Issue #6 fix: install redistributable packages after game verification**

New phase added to `GameDownloadViewModel.RunAsync()` after download/verification:
1. `Services/RedistInstallService.cs` (new) — scans `{gameDir}\_CommonRedist` for `DirectX/DXSETUP.exe` and `vcredist/**/vcredist_x64/x86.exe`, runs each silently (`/silent`, `/install /quiet /norestart`).
2. `Models/LauncherSettings.cs` — added `RedistInstalledForPath` (string?) to avoid re-running on every launch.
3. `ViewModels/GameDownloadViewModel.cs` — added `NeedRedistInstall` + `OnRedistCompleted` props; new `RunRedistIfNeededAsync()` method called after verification/download phase, shows "Установка компонентов..." status.
4. `ViewModels/MainWindowViewModel.cs` — injects `RedistInstallService`, computes `NeedRedistInstall` from settings, persists `RedistInstalledForPath` via `OnRedistCompleted`.
5. `App.axaml.cs` — registered `RedistInstallService` as singleton.
6. `Preview/PreviewRegistry.cs` — updated `GameDownload` preview stubs to pass `new RedistInstallService()`.

---

## Previous Focus

**Issue #9 fix: slow game file scanning on HDD** — two-part optimization:
1. `Services/LocalManifestCache.cs` (new) — persists `(size, mtime, MD5)` per file to `%LocalAppData%\d2c-launcher\local_manifest_cache.json`. On subsequent scans, files whose size and last-write timestamp match the cache skip MD5 computation entirely.
2. `Services/LocalManifestService.cs` (modified) — uses the cache + `GetOptimalParallelism()` via WMI: `Win32_LogicalDisk → Win32_DiskPartition → Win32_DiskDrive → MSFT_PhysicalDisk.MediaType`. MediaType 4 (SSD) → parallel reads; HDD/unknown → sequential (parallelism=1) to avoid disk-head thrashing.

---

## Previous Focus

**Issue #4 fix: launcher process doesn't exit cleanly** — three-part fix:
1. `Integration/SteamManager.cs` — track `_activeBridgeProcess` field; kill it immediately in `Dispose()` before waiting on the monitor task, and clear it in a `finally` block after the bridge exits or is killed.
2. `Program.cs` — added `Environment.Exit(0)` after `FaroTelemetryService.ShutdownAsync()` to force-kill any lingering foreground threads (e.g. from SocketIOClient).
3. `Services/FaroTelemetryService.cs` — added `ConfigureAwait(false)` to `FlushAsync()` and `ShutdownAsync()` to prevent a potential deadlock when called from `Main()` after the Avalonia SynchronizationContext is torn down.

---

## Previous Focus: Settings modal redesign

**Settings modal redesign** — issue #3 complete. Replaced the old 2-tab unstyled settings panel with a redesigned 3-tab modal.

### What Was Changed (issue #3)
- `Models/GameLaunchSettings.cs` — Added `Fullscreen`, `ResolutionWidth`, `ResolutionHeight`
- `Models/LauncherSettings.cs` — Added `MaxDownloadSpeedKbps`, `AutoUpdate`
- `Models/CvarSettings.cs` — Added `QuickCast`, `ChatFilter`
- `Services/CfgGenerator.cs` — Added fullscreen/resolution to CLI args
- `Services/CvarMapping.cs` — Added `dota_quick_select_setting`, `chat_filter_enabled`
- `ViewModels/SettingsViewModel.cs` — Injected `ISettingsStorage`; added `GameDirectory`, `FolderSizeText`, `RefreshGameDirectory()`, `Fullscreen`, `SelectedResolutionIndex`, `QuickCast`, `ChatFilter`, `AutoUpdate`, `DownloadSpeedLimitText`
- `ViewModels/MainLauncherViewModel.cs` — Pass `settingsStorage` to `SettingsViewModel`
- `Views/Components/SettingsPanel.axaml` — Complete redesign: fixed 460×520px, dark themed, 3 tabs (ВИЗУАЛЬНЫЕ/ГЕЙМПЛЕЙ/СЕТЬ), directory block, scrollable content, no Save/Cancel footer
- `Views/Components/SettingsPanel.axaml.cs` — Removed `SetGameDirectory` (now ViewModel-bound)
- `Views/MainLauncherView.axaml.cs` — Call `vm.Settings.RefreshGameDirectory()` instead of `SetGameDirectory`
- `Preview/PreviewRegistry.cs` — Added `StubSettingsStorage` to SettingsPanel preview

---

## Previous Focus: Game download / verification flow

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
| issue #2 | Windows Defender exclusion for game folder — `WindowsDefenderService.TryAddExclusionAsync`, `DefenderExclusionPath` in `LauncherSettings`, wired in `MainWindowViewModel.ShowGameDownload()` |
| `e87b6e3` | Fix issue #1: double-dispose guard in `SteamManager` — add `_disposed` bool to prevent `ObjectDisposedException` on shutdown |
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
