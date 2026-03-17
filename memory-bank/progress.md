# Progress

## Feature Status

### Core Infrastructure


| Feature                                    | Status         | Notes                                           |
| ------------------------------------------ | -------------- | ----------------------------------------------- |
| Steam auth via SteamBridge subprocess      | ✅ Done         | 12s timeout, exponential backoff                |
| Clean shutdown (issue #4)                  | ✅ Done         | Bridge kill on Dispose, Environment.Exit(0), ConfigureAwait fixes |
| App state routing (Steam → GameDir → Main) | ✅ Done         | `MainWindowViewModel`                           |
| DI container setup                         | ✅ Done         | `App.axaml.cs`                                  |
| Settings persistence (launcher JSON)       | ✅ Done         | `%AppData%\d2c-launcher\launcher_settings.json` |
| Application auto-updates                   | ✅ Done         | Velopack + GitHub Releases                      |
| Hardware info logging + HWID               | ✅ Done         | `HardwareInfoService` committed; HWID not yet sent to backend (backlog) |


### Matchmaking


| Feature                        | Status |
| ------------------------------ | ------ |
| Queue enter / leave            | ✅ Done |
| Real-time queue state updates  | ✅ Done |
| Ready check (accept / decline) | ✅ Done |
| Party invite / accept / leave  | ✅ Done |
| Floating invite notifications  | ✅ Done |
| Server search status           | ✅ Done |


### Game Management


| Feature                           | Status | Notes                              |
| --------------------------------- | ------ | ---------------------------------- |
| Game directory validation         | ✅ Done |                                    |
| Game download / verify on launch  | ✅ Done | `GameDownloadView`, manifest diff, HTTP download |
| HDD scan optimization (issue #9)  | ✅ Done | mtime+size hash cache + WMI SSD detection for parallelism |
| Scan duration metric to Faro (issue #10) | ✅ Done | `TrackEvent("scan_completed")` with `duration_ms` + `file_count` |
| Redist install after verify (issue #6) | ✅ Done | `RedistInstallService` — runs `_CommonRedist` DirectX + vcredist silently once per game dir |
| Game launch with Source 1 args    | ✅ Done |                                    |
| Runtime console command injection | ✅ Done | WM_COPYDATA via P/Invoke           |
| Bi-directional config.cfg sync    | ✅ Done | Two-phase: host_writeconfig + read |
| Settings UI (cvars)               | ✅ Done |                                    |


### Launch Flags (CLI arguments)


| Setting              | Status |
| -------------------- | ------ |
| Language (-language) | ✅ Done |
| NoVid (-novid)       | ✅ Done |


### Game Settings (cvars → config.cfg)


| Setting                  | Cvar                                             | Status |
| ------------------------ | ------------------------------------------------ | ------ |
| FPS cap                  | `fps_max`                                        | ✅ Done |
| Console enabled          | `con_enable`                                     | ✅ Done |
| Disable camera zoom      | `dota_camera_disable_zoom`                       | ✅ Done |
| Force right click attack | `dota_force_right_click_attack`                  | ✅ Done |
| Auto-repeat right mouse  | `dota_player_auto_repeat_right_mouse`            | ✅ Done |
| Camera reset on spawn    | `dota_reset_camera_on_spawn`                     | ✅ Done |
| Auto-attack mode         | `dota_player_units_auto_attack` + `_after_spell` | ✅ Done |
| Colorblind mode          | (composite)                                      | ✅ Done |


### Chat

| Feature | Status | Notes |
| ------- | ------ | ----- |
| Chat panel UI | ✅ Done | `ChatPanel.axaml`, `ChatViewModel` |
| Chat SSE live updates (issue #12) | ✅ Done | `SubscribeChatAsync` via `IAsyncEnumerable`; 3s reconnect backoff |
| Rich message rendering (issue #15) | ✅ Done | `RichMessageParser` + `RichMessageBlock`; emoticons, rarity tags, clickable URLs |
| Embed images in chat (issue #20) | ✅ Done | `ImageSegment`; auto-renders `.jpg/.png/.gif/.webp` URLs as inline images (max 200×200) |
| Message updates via SSE (issue #24) | ✅ Done | `message_updated` event updates existing message content in-place |
| Emoticons as animated GIFs (issue #17) | ✅ Done | `Avalonia.Labs.Gif.GifImage`; `IHttpImageService`/`HttpImageService` |
| Emoticon disk cache (issue #33) | ✅ Done | `IEmoticonService`/`EmoticonService`; `%LocalAppData%\d2c-launcher\emoticons\`; 24h TTL |
| Chat scrolling | 🔲 Open | Issue #13 |

### Live Matches (issue #80)

| Feature | Status | Notes |
| ------- | ------ | ----- |
| Live match list (`GET /v1/live/list`) | ✅ Done | `LiveViewModel` polls every 5s |
| Per-match detail polling (`GET /v1/live/{id}`) | ✅ Done | `SubscribeLiveMatchAsync` polls every 3s via `IAsyncEnumerable` |
| Minimap with animated hero positions | ✅ Done | `LivePanel.axaml` Canvas + Avalonia Transitions on `Canvas.Left`/`Canvas.Top` (1s ease) |
| Radiant/Dire player lists | ✅ Done | Hero icon, name, K/D/A, level |
| Preview component | ✅ Done | `LivePanel` in `PreviewRegistry` |

### UI / UX

| Feature | Status | Notes |
| ------- | ------ | ----- |
| Gamemode flags icons (issue #30) | ✅ Done | Proper icons for game modes in `GameSearchPanel` |
| Online player count (issue #32) | ✅ Done | `OnlineStatsText` bound in `MainLauncherView`; "X в игре, Y на сайте" |
| Consistent online indicator + `PlayerPreviewControl` | ✅ Done | Reusable control; unified across Party, Queue |
| Panel header typography unification | ✅ Done | Chat, Party, Game Search headers unified |
| Release notes in update banner | ✅ Done | `feat: display release notes in update banner` |
| Matchmaking telemetry events to Faro | ✅ Done | `feat: add matchmaking telemetry events to Faro` |
| Close to tray (issue #46) | ✅ Done | X button hides to tray; tray menu Open/Exit; second instance restores; match found restores window automatically; `CloseToTray` setting (default true) |
| Reply preview in chat messages (issue #50) | ✅ Done | Blue left-border preview block above message; shows quoted author + truncated text; from `ThreadMessageDTO.Reply` via REST and SSE |


### Game Settings (cvars → config.cfg)

| Setting | Cvar | Status |
| ------- | ---- | ------ |
| FPS cap | `fps_max` | ✅ Done |
| Console enabled | `con_enable` | ✅ Done |
| Disable camera zoom | `dota_camera_disable_zoom` | ✅ Done |
| Force right click attack | `dota_force_right_click_attack` | ✅ Done |
| Auto-repeat right mouse | `dota_player_auto_repeat_right_mouse` | ✅ Done (issue #21 under investigation) |
| Camera reset on spawn | `dota_reset_camera_on_spawn` | ✅ Done |
| Auto-attack mode | `dota_player_units_auto_attack` + `_after_spell` | ✅ Done |
| Colorblind mode | (composite) | ✅ Done |
| Camera distance (issue #19) | `dota_camera_distance` | ✅ Done; nullable, clamped [1000,1600] |
| Quick cast | `dota_quick_select_setting` | ✅ Done |
| Chat filter | `chat_filter_enabled` | ✅ Done |

### Testing

| Area | Status |
| ---- | ------ |
| xUnit project setup | ✅ Done |
| `CfgGenerator` tests | ✅ Done |
| `DotaCfgReader` tests | ✅ Done |
| `CvarMapping` tests | ✅ Done |
| `DotaCfgWriter` tests (issue #0e31d40) | ✅ Done |
| CI test step | ✅ Done |
| Integration testing research | ✅ Done — see `docs/integration-testing-plan.md` |
| NSubstitute + `ISteamManager` extraction | 🔲 Planned |
| `FakeQueueSocketService` | 🔲 Planned |
| ViewModel integration tests (Avalonia.Headless.XUnit) | 🔲 Planned |


### CI/CD


| Step                    | Status | Notes |
| ----------------------- | ------ | ----- |
| Build workflow          | ✅ Done | |
| Test step               | ✅ Done | |
| Velopack packaging      | ✅ Done | |
| Nightly channel         | ✅ Done | Every master push → `nightly` pre-release (version `0.0.{run_number}`, channel `nightly`); opt-in via `NightlyUpdates` in settings JSON |
| Stable release          | ✅ Done | Manual `v*.*.*` tag → versioned stable release; `git tag vX.Y.Z && git push origin vX.Y.Z` |


---

## Known Gaps / Backlog

Open GitHub issues:

| Issue | Title | Priority |
| ----- | ----- | -------- |
| #31 | Add proper localization | enhancement |
| #23 | Add support for abandoning in launcher | — |
| #22 | Steam not being detected | under investigation — root cause unknown |
| #21 | Setting autorepeat doesn't work | under investigation — may not be a bug |
| #18 | Parallelize local scan + remote manifest load | enhancement |
| #13 | Support chat scrolling | — |
| #8 | Research crash dump analysis | — |
| #7 | Handling game crashes | — |

Other known technical debt:

| Item | Notes |
| ---- | ----- |
| Keybind settings UI | `config.cfg` bind lines parsed but not exposed in UI |
| Send HWID to backend | `HardwareInfoService` logs HWID but doesn't send it |
| Extra launch args UI | `ExtraArgs` field in model; no UI |
| Custom cfg lines UI | `CustomCfgLines` wired to `d2c_launch.cfg`; no UI |
| `QueueSocketService.Dispose()` blocks UI | `Task.Run+.Wait` up to 2s |
| Chat thread ID hardcoded | `"17aa3530-d152-462e-a032-909ae69019ed"` in `ChatViewModel` |

---

## Architecture / Docs Status

| Doc | Status |
| --- | ------ |
| `docs/source-engine-launch.md` | ✅ Written |
| `docs/source-engine-config-persistence.md` | ✅ Written |
| `docs/settings-architecture.md` | ✅ Written |
| `docs/game-update-manifest.md` | ✅ Written |
| `docs/client-dll-patching.md` | ✅ Written — patching done server-side (CDN); enables `dota_camera_distance` cvar; released |
| Memory bank (`memory-bank/`) | ✅ Written |


