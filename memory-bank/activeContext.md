# Active Context

## Current Focus

**Issue #92: Achievement notification** — implemented.

### What Was Changed
- `Services/IBackendApiService.cs` — added `AcknowledgeNotificationAsync(string notificationId)`
- `Services/BackendApiService.cs` — implemented via `NotificationController_acknowledgeAsync`
- `Resources/Strings.cs` — added `AchievementUnlocked` and `OpenAchievements`
- `ViewModels/AchievementToastViewModel.cs` — new toast VM; shows achievement title, description, and image (mapped from `notification.achievement.key` integer 0–29 to a static image URL table); `OpenCommand` opens `https://dotaclassic.ru/players/{steamId}/achievements` in browser; `Closed` event acknowledges notification via REST API
- `ViewModels/NotificationAreaViewModel.cs` — added `AddAchievementToast(NotificationDto, IBackendApiService)` method
- `Integration/SocketEventCoordinator.cs` — injected `IBackendApiService`; subscribed to `NotificationCreated`; routes `ACHIEVEMENT_COMPLETE` notifications to `NotificationArea.AddAchievementToast`
- `ViewModels/MainLauncherViewModel.cs` — passes `backendApiService` to `SocketEventCoordinator`
- `Views/Components/NotificationArea.axaml` — added `DataTemplate` for `AchievementToastViewModel` (achievement image + gold title + description + blue "ДОСТИЖЕНИЯ" button)
- `Preview/PreviewStubs.cs` — stub `AcknowledgeNotificationAsync` (no-op)

---

## Previous Focus

**Issue #93: Launch game with +connect when not running** — implemented.

### What Was Changed
- `ViewModels/GameLaunchViewModel.cs` — `LaunchGame` now accepts optional `string? launchOptions` (any extra `+commands` appended last to the argument list). `ConnectToGameAsync`: when the game is not running, calls `LaunchGame($"+connect {url}")` and returns immediately; when the game is already running, keeps the existing WM_COPYDATA path.

---

## Previous Focus

**Issue #88: Split SettingsViewModel into focused sub-ViewModels** — implemented.

### What Was Changed
- `ViewModels/GameSettingsViewModel.cs` — new; owns video settings (fullscreen, borderless, resolution picker) + all gameplay cvars + `PushCvar` delegate + `RefreshFromCvarProvider/Video`; `ResolutionItem` record moved here
- `ViewModels/LauncherPrefsViewModel.cs` — new; owns launch params (language, NoVid, ExtraArgs) + launcher prefs (AutoUpdate, CloseToTray, AutoConnectToGame, UiScale, DefenderExclusionEnabled) + game directory display
- `ViewModels/DlcViewModel.cs` — new; owns DLC package selection, change detection, apply
- `ViewModels/SettingsViewModel.cs` — reduced to thin container (~45 lines); exposes `GameSettings`, `LauncherPrefs`, `Dlc` properties; delegates all public methods/callbacks to sub-VMs; same constructor signature → no callers changed
- `Views/Components/Settings/VideoSettingsView.axaml(.cs)` — new; Визуальные tab content; `x:DataType="GameSettingsViewModel"`; code-behind handles `ApplyMonitorSize`
- `Views/Components/Settings/GameplayView.axaml(.cs)` — new; Геймплей tab content; `x:DataType="GameSettingsViewModel"`; code-behind handles camera distance key/pointer events
- `Views/Components/Settings/LauncherPrefsView.axaml(.cs)` — new; Лаунчер tab + directory block (moved from SettingsPanel top); `x:DataType="LauncherPrefsViewModel"`; raises `SettingsPanel.SelectDirectoryRequestedEvent`; language/NoVid moved here from Визуальные tab
- `Views/Components/Settings/DlcView.axaml(.cs)` — new; ДЛС tab content; `x:DataType="DlcViewModel"`
- `Views/Components/SettingsPanel.axaml` — reduced to tab shell (~90 lines); all styles kept here for cascade; each tab binds `DataContext` to its sub-VM
- `Views/Components/SettingsPanel.axaml.cs` — reduced to event definition only; all handlers moved to sub-views

---

## Previous Focus

**Issue #90: FakeSteamManager and integration tests** — implemented.

### What Was Changed
- `d2c-launcher.Tests/Fakes/FakeSteamManager.cs` — new hand-written `ISteamManager` fake with `SimulateStatusUpdate`, `SimulateUserUpdated`, `SimulateAuthTicket`, `SimulatePoll` helpers
- `d2c-launcher.Tests/SteamStateTransitionTests.cs` — 12 new tests: initial-state derivation from `FakeSteamManager` properties (mirrors `MainWindowViewModel` constructor logic), event-driven state transition assertions, and `AuthCoordinator` integration tests using `FakeSteamManager`

Note: `MainWindowViewModel` itself cannot be instantiated in the test project because it uses `Dispatcher.UIThread` directly. Full ViewModel integration tests require `Avalonia.Headless.XUnit` in a separate project (Layer 2 of integration testing plan).

---

## Previous Focus

**Issue #85: Memory leak in QueueViewModel** — fixed.

### What Was Changed
- `ViewModels/QueueViewModel.cs` — `RefreshMatchmakingModesAsync` now reuses existing `MatchmakingModeView` instances (by `ModeId`) instead of always creating new ones. `PropertyChanged` is subscribed only once per mode object (on first creation). Modes added/removed by the backend are still handled correctly.

---

## Previous Focus

**Issue #80: Live matches tab** — implemented.

### What Was Changed
- `Services/IBackendApiService.cs` — added `GetLiveMatchesAsync()` and `SubscribeLiveMatchAsync(matchId, ct)`
- `Services/BackendApiService.cs` — implemented both: `GetLiveMatchesAsync` wraps generated `LiveMatchController_listMatchesAsync`; `SubscribeLiveMatchAsync` polls `LiveMatchController_liveMatchAsync` every 3s as an `IAsyncEnumerable`
- `ViewModels/HeroOnMapViewModel.cs` — new; `CanvasLeft`/`CanvasTop` computed from `pos_x`/`pos_y` using same remap as webapp; hero icon URL from Steamstatic CDN
- `ViewModels/LivePlayerRowViewModel.cs` — new; flat row for player list (name, hero icon, K/D/A, level)
- `ViewModels/LiveMatchCardViewModel.cs` — new; holds heroes + player lists; `UpdateFrom(LiveMatchDto)` mutates existing `HeroOnMapViewModel` instances in place so Avalonia transitions fire
- `ViewModels/LiveViewModel.cs` — new; polls match list every 5s; runs detail polling loop for selected match; auto-selects first match
- `ViewModels/MainLauncherViewModel.cs` — added `Live` child VM; disposed in `Dispose()`
- `Views/Components/LivePanel.axaml/.cs` — new; match sidebar + minimap Canvas + team player lists; hero borders have `DoubleTransition` on `Canvas.Left`/`Canvas.Top` for smooth 1s position animation
- `Util/TeamColorConverter.cs` — new; team 2 → radiant yellow-green, team 3 → dire red (border color)
- `Views/MainLauncherView.axaml` — replaced "Live — скоро" placeholder with `<components:LivePanel>`
- `Preview/PreviewStubs.cs` — added stub `GetLiveMatchesAsync` and `SubscribeLiveMatchAsync`
- `Preview/PreviewRegistry.cs` — added `LivePanel` entry

---

## Previous Focus

**Issue #76: Handle failed game mode loading** — implemented.

### What Was Changed
- `ViewModels/QueueViewModel.cs` — added `_modesRefreshTimer` (`DispatcherTimer`, 30s interval) that calls `RefreshMatchmakingModesAsync()` on each tick; stopped in `Dispose()`. The existing method already merges in-memory selection state (`existing?.IsSelected`) so re-fetching is safe.

---

## Previous Focus

**Issue #78: Handle PLAYER_PARTY_STATE socket event** — implemented.

### What Was Changed
- `Services/IBackendApiService.cs` — added `PartySnapshot MapPartyDto(PartyDto party)`
- `Services/BackendApiService.cs` — extracted `MapPartyDto(PartyDto)` from `GetMyPartySnapshotAsync`; `GetMyPartySnapshotAsync` now delegates to it
- `ViewModels/PartyViewModel.cs` — extracted `ApplyPartySnapshot(PartySnapshot)` from `RefreshPartyAsync`; `PartyUpdated` handler now calls `ApplyPartySnapshot(_backendApiService.MapPartyDto(party))` directly instead of making an extra HTTP call
- `Preview/PreviewStubs.cs` — added stub `MapPartyDto`

---

## Previous Focus

**Issue #23: Abandon game support** — implemented.

### What Was Changed
- `Services/IBackendApiService.cs` — added `AbandonGameAsync()`
- `Services/BackendApiService.cs` — implemented via `PlayerController_abandonGameAsync`
- `ViewModels/MainLauncherViewModel.cs` — added `CanAbandonGame` (excludes mode 1=unranked 5x5 and mode 8=highroom), `IsAbandonConfirmOpen`, `RequestAbandonGameCommand`, `CancelAbandonGameCommand`, `ConfirmAbandonGameCommand`; subscribes to `Launch.HasServerUrl` and `Room.RoomMode` changes
- `Views/MainLauncherView.axaml` — removed "Сервер: {url}" hint border; added "X" abandon button (red, 52×80) to the right of QueueButton (visible only when `CanAbandonGame`); added abandon confirmation overlay (ZIndex=210)
- `Preview/PreviewStubs.cs` — added stub `AbandonGameAsync`

---

## Previous Focus

**Integration testing research** — completed. Plan saved to `docs/integration-testing-plan.md`.

Key findings:
- 195 unit tests, all passing; no mocking library yet (need NSubstitute)
- Most services have interfaces already; `SteamManager` is the main blocker (no `ISteamManager`)
- Layered plan: Layer 1 = service-level fakes (no Avalonia), Layer 2 = ViewModel tests with `Avalonia.Headless.XUnit`, Layer 3 = full HTTP/WS mocks
- Next steps: add NSubstitute, extract `ISteamManager`, add `D2C_API_URL` env var, write `FakeQueueSocketService`

---

## Previous Focus

**Issue #71: Persist selected game modes** — implemented.

### What Was Changed
- `Models/LauncherSettings.cs` — added `List<int>? SelectedModeIds`; null = first run, defaults to mode 7 (Bots)
- `ViewModels/QueueViewModel.cs` — injected `ISettingsStorage`; `RefreshMatchmakingModesAsync` loads initial selection from settings (or defaults to `[7]`); each `MatchmakingModeView.IsSelected` change triggers `PersistSelectedModes()` which saves to settings
- `ViewModels/MainLauncherViewModel.cs` — passes `settingsStorage` to `QueueViewModel` constructor
- `Preview/PreviewRegistry.cs` — updated two `QueueViewModel` instantiations to pass `StubSettingsStorage`

---

## Previous Focus

**Issue #66: Toast/notification system** — implemented.

### What Was Changed
- `ViewModels/NotificationViewModel.cs` — new abstract base: auto-dismiss timer, `RemainingSeconds`, `Closed` event, `ForceClose()`, `StopTimer()`
- `ViewModels/SimpleToastViewModel.cs` — new: plain text toast, inherits base, auto-dismisses
- `ViewModels/PartyInviteNotificationViewModel.cs` — now inherits `NotificationViewModel`; removed its own timer, delegates timer/close to base
- `ViewModels/NotificationAreaViewModel.cs` — collection changed from `ObservableCollection<PartyInviteNotificationViewModel> Invites` to `ObservableCollection<NotificationViewModel> Notifications`; added `AddToast(message, seconds)` method
- `Views/Components/NotificationArea.axaml` — uses `ItemsControl.DataTemplates` with type-keyed templates for `PartyInviteNotificationViewModel` and `SimpleToastViewModel`
- `ViewModels/QueueViewModel.cs` — added `Action? ShowNoModesSelectedToast` property; fires it when queue pressed with 0 modes
- `ViewModels/MainLauncherViewModel.cs` — wires `Queue.ShowNoModesSelectedToast` → `NotificationArea.AddToast("Выберите хотя бы один режим игры для поиска")`

---

## Previous Focus

**Issue #57: Profile page** — implemented.

### What Was Changed
- `Models/PlayerProfileData.cs` — new record (name, avatar, W/L/D, MMR, rank, avg KDA)
- `Models/HeroProfileData.cs` — new record (hero name, games, win rate, KDA)
- `Services/IBackendApiService.cs` — added `GetPlayerSummaryAsync` and `GetHeroStatsAsync`
- `Services/BackendApiService.cs` — implemented both methods using generated client; `FormatHeroName` helper strips "npc_dota_hero_" prefix and capitalizes
- `ViewModels/ProfileViewModel.cs` — new VM with `LoadAsync(steamId)`, exposes stats + `ObservableCollection<HeroRowViewModel> TopHeroes`
- `ViewModels/MainLauncherViewModel.cs` — added `Profile` child VM, `IsProfileOpen`, `OpenProfile()`, `CloseProfile()`
- `Views/Components/ProfilePanel.axaml/.cs` — new: header stats bar + KDA panel + top heroes table
- `Views/MainLauncherView.axaml` — swaps main content with `ProfilePanel` when `IsProfileOpen`; shows queue indicator banner when searching
- `Views/Components/LauncherHeader.axaml/.cs` — avatar/name block is now a `Button` that toggles profile open/closed
- `Preview/PreviewStubs.cs` — stub implementations for new API methods

---

## Previous Focus

**Release infrastructure** — nightly/stable channel split implemented and live.

- Every master push → `nightly` pre-release (Velopack channel `nightly`, version `0.0.{run_number}`)
- Manual `v*.*.*` tag → stable versioned release
- `LauncherSettings.NightlyUpdates` (default false) — set in JSON to opt into nightly channel
- See `docs/release-cycle.md` for full details

---

## Recent Completed Work

### Refactoring: MainLauncherViewModel (commit c46fbdf)
- Extracted `Services/AuthCoordinator.cs` — owns token application, queue connect/disconnect, settings persistence, Steam auth event subscription
- Extracted `Integration/SocketSoundCoordinator.cs` — owns 4 socket event → sound/notification handlers
- MLVM reduced from 377 → 285 lines

### Issue #50: Reply preview in chat messages (commit 5ace64d)
- Messages with a reply show a blue left-border preview block above content
- Displays quoted author name + truncated text
- Reply data from `ThreadMessageDTO.Reply` in both REST (`GetChatMessagesAsync`) and SSE (`ParseSseChatMessage`) paths

### Issue #46: Close to tray (commit cc35153)
- `IWindowService` / `WindowService` — `ShowAndActivate()`
- X button hides to tray; tray menu "Открыть" / "Выход"
- Second instance → `__show__` pipe message → `ShowAndActivate()`
- Match found / room ready → auto-restores window from tray
- `CloseToTray` setting in `LauncherSettings` (default true)

---

## Previous Focus

**Issue #40: dota_camera_distance not persisting**

Root cause: the game client overwrites `config.cfg` on exit, wiping any cvars it doesn't manage (like `dota_camera_distance`).

Fix: introduced `CvarConfigSource` enum (`ConfigCfg` / `PresetCfg`) on `CvarEntry`. Cvars the game client doesn't manage are now written to `d2c_preset.cfg` instead of `config.cfg`. The preset file is exec'd at launch and never touched by the game client.

Changes:
- `Services/CvarMapping.cs` — added `CvarConfigSource` enum; `CvarEntry` gets `Source` field (default `ConfigCfg`); `dota_camera_distance` marked `PresetCfg`
- `Services/DotaCfgReader.cs` — extracted `ReadKnownCvarsFromFile(path)`; `ApplyToSettings` takes `CvarConfigSource` param and reads the appropriate file; startup now reads both files
- `Services/CfgGenerator.cs` — `WritePreset` accepts optional `userCvars` dict; appends them after hardcoded lines
- `Services/CvarSettingsProvider.cs` — `Update()` always writes (dropped `!IsGameRunning` guard); splits cvars by source and writes to correct files; `LoadFromConfigCfg` reads both files; exposes `GetPresetCvars()`
- `Services/ICvarSettingsProvider.cs` — added `GetPresetCvars()` method; updated doc comments
- `ViewModels/GameLaunchViewModel.cs` — passes `_cvarProvider.GetPresetCvars()` to `WritePreset` on launch
- `Preview/PreviewStubs.cs` — stub implements `GetPresetCvars()`
- `docs/settings-architecture.md` — documented the cfg file split

---

## Previous Focus

**Issue #38: Add mod limitations**

Implemented game mode access restrictions in the matchmaking mode selector:

- `Models/PartyMemberView.cs` — added `int? Mmr` (nullable; null = unknown, e.g. party leader)
- `Services/BackendApiService.cs` — passes `mmr: (int)summary.SeasonStats.Mmr` when constructing `PartyMemberView` for party players
- `ViewModels/QueueViewModel.cs` — replaced `CanMemberPlayMode` + `FormatMemberRestriction` with single `GetMemberModeRestriction(member, modeId) → string?` that returns the specific reason:
  - Permaban → "Аккаунт заблокирован навсегда"
  - Temp ban (human modes only) → "Поиск запрещён до {date}"
  - `!CanPlayHumanGames` → "Для доступа выиграйте хотя бы одну игру"
  - `!CanPlaySimpleModes` → "Сыграйте против ботов для открытия режима"
  - Highroom (mode 8) with `Mmr < 2500` → "Нужно 2500 MMR (у {name}: {mmr})"

---

## Previous Focus

**Issue #37: First-run introduction overlay**

Added one-time onboarding overlay for new players + user indicator in header.

Files changed:
- `Models/LauncherSettings.cs` — added `bool IntroShown`
- `ViewModels/MainLauncherViewModel.cs` — added `IsIntroOpen`, `IntroStep`, `IntroStepCount`, `NextIntroStepCommand`, `CloseIntroCommand`; initialized from `!settings.IntroShown`; `CloseIntro()` saves `IntroShown = true`
- `Views/MainLauncherView.axaml` — intro overlay (ZIndex=200) with 4 step panels, next/skip buttons; uses `IntroStepConverter` and `IntroNextButtonTextConverter`
- `Util/IntroConverters.cs` — new: `IntroStepConverter` (equality check), `IntroNextButtonTextConverter` ("Далее"/"Начать играть")
- `Views/Components/LauncherHeader.axaml` — user block now shows green online dot on avatar + "Вы:" sublabel above persona name

Open issues (as of 2026-03-09):
- #31 — Add proper localization (enhancement)
- #23 — Add support for abandoning in launcher
- #22 — Steam not being detected
- #21 — Setting autorepeat doesn't work (bug)
- #18 — Parallelize local file scan + remote manifest load
- #13 — Support chat scrolling
- #8 — Research crash dump analysis
- #7 — Handling game crashes

---

## Previous Focus

**Issue #19: Camera distance setting via cvar**

Added `dota_camera_distance` as a configurable setting in the ГЕЙМПЛЕЙ settings tab.

- `Models/CvarSettings.cs` — added `int? CameraDistance` (nullable; null = not set, game uses its default of 1134)
- `Services/CvarMapping.cs` — added `dota_camera_distance` entry; clamps to [1000, 1600] on read; omits from cfg when null
- `ViewModels/SettingsViewModel.cs` — added `CameraDistanceText` string property with clamp and live `PushCvar`; added to `CvarPropertyNames`
- `Views/Components/SettingsPanel.axaml` — added TextBox row in ГЕЙМПЛЕЙ tab with warning about values > 1134 causing unclickable screen areas

---

## Previous Focus

**Issue #33: Cache emoticons**

Introduced `IEmoticonService` / `EmoticonService` — a dedicated service that owns emoticon lifecycle:
- Cache dir: `%LocalAppData%\d2c-launcher\emoticons\`
- Each emoticon stored as `{code}.gif` + `{code}.gif.meta` (JSON with `CachedAt` timestamp)
- TTL: 24 hours — stale entries are re-downloaded on next startup
- Cleanup: emoticons no longer in the server list are deleted from disk
- `ChatViewModel` now injects `IEmoticonService`; `LoadEmoticonsAsync` is a single `await _emoticonService.GetEmoticonImagesAsync()` call

Files changed:
- `Services/IEmoticonService.cs` — new interface
- `Services/EmoticonService.cs` — new implementation (cache + TTL + cleanup)
- `App.axaml.cs` — registered `IEmoticonService → EmoticonService` as singleton
- `ViewModels/ChatViewModel.cs` — inject `IEmoticonService`, simplified `LoadEmoticonsAsync`
- `ViewModels/MainLauncherViewModel.cs` — added `IEmoticonService` param, stores field, passes to `ChatViewModel`
- `ViewModels/MainWindowViewModel.cs` — added `IEmoticonService` param, stores field, passes to `MainLauncherViewModel`
- `Preview/PreviewStubs.cs` — added `StubEmoticonService`
- `Preview/PreviewRegistry.cs` — passes `StubEmoticonService` to `ChatViewModel`

---

## Previous Focus

**Issue #20: Embed images in chat**

Image URLs in chat messages now render as embedded images instead of clickable text links.

- `Models/RichSegment.cs` — Added `ImageSegment` with `Url` property
- `Util/RichMessageParser.cs` — Added `s_imageUrl` regex (before generic `s_url` rule) matching `.jpg/.jpeg/.png/.gif/.webp/.bmp` URLs → `ImageSegment`
- `Views/Components/RichMessageBlock.cs` — Added `ImageSegment` case: renders as `Image` (max 200×200) via `InlineUIContainer`; async-loads bytes with static `HttpClient`, dispatches bitmap to UI thread; silent fail on error

---

## Previous Focus

**Issue #24: message_updated event doesn't update rendered messages**

`ConsumeIncomingMessage` was skipping SSE messages whose `MessageId` already existed in `Messages`. Fixed by updating the existing entry's `Content` and `RichContent` instead of returning early. Also made `Content` an `[ObservableProperty]` in `ChatMessageView` so bindings react to the change.

Files changed:
- `Models/ChatMessageView.cs` — `Content` changed from `{ get; }` to `[ObservableProperty]`
- `ViewModels/ChatViewModel.cs` — `ConsumeIncomingMessage` now updates existing message instead of skipping

---

## Previous Focus

**Issue #32: Show online player count**

Added `TextBlock` bound to `OnlineStatsText` below the `QueueButton` in [Views/MainLauncherView.axaml](Views/MainLauncherView.axaml). The ViewModel already had full logic: `OnlineInGame` polled from `/v1/stats/online` every 5s, `OnlineSessions` from the `ONLINE_UPDATE` websocket event (length of `online` array), combined into `OnlineStatsText` = "X в игре, Y на сайте". Only the XAML binding was missing.

---

## Previous Focus

**Issue #17: Emoticons render as static images, not as GIFs**

Root cause: emoticons downloaded with `new Bitmap(stream)` which decodes only the first frame.

Fix: replaced `Bitmap`-based emoticon rendering with `Avalonia.Labs.Gif.GifImage` (v11.3.1), feeding a `MemoryStream` of raw bytes directly to `GifImage.Source`. Image loading extracted from `BackendApiService` into dedicated `IHttpImageService`/`HttpImageService`.

Files changed:
- `d2c-launcher.csproj` — added `Avalonia.Labs.Gif` v11.3.1
- `Services/IHttpImageService.cs` + `HttpImageService.cs` — new: `LoadBitmapAsync`, `LoadBytesAsync`
- `Services/IBackendApiService.cs` / `BackendApiService.cs` — removed image-loading methods; internal `TryLoadAvatarAsync` stays private
- `Models/RichSegment.cs` — `EmoticonSegment.Image: Bitmap?` → `Bytes: byte[]?`
- `Util/RichMessageParser.cs` — emoticons dict type `Bitmap` → `byte[]`
- `ViewModels/ChatViewModel.cs` — injects `IHttpImageService`; uses it for emoticon bytes and avatar bitmaps
- `ViewModels/NotificationAreaViewModel.cs` — injects `IHttpImageService` (was `IBackendApiService`)
- `ViewModels/MainLauncherViewModel.cs` — added `IHttpImageService` param; passes down
- `ViewModels/MainWindowViewModel.cs` — added `IHttpImageService` field; passes to launcher
- `Views/Components/RichMessageBlock.cs` — emoticon rendered as `GifImage { Source = new MemoryStream(bytes) }`
- `App.axaml.cs` — registered `IHttpImageService → HttpImageService`
- `Preview/PreviewStubs.cs` — added `StubHttpImageService`
- `Preview/PreviewRegistry.cs` — passes `StubHttpImageService` to affected VMs

Note: `Avalonia.Labs.Gif` v11.3.1 only has `GifImage` (no `GifStreamSource`). `GifImage.Source` accepts `Stream`, `Uri`, or `string`.

---

## Previous Focus

**Issue #27: Fix build warnings**

Achieved 0 warnings / 0 errors. Changes made:
- `d2c-launcher.csproj` — `net10.0` → `net10.0-windows` (eliminates CA1416 platform warnings); Velopack `0.0.1099` → `0.0.1251` (eliminates NU1603)
- `Services/BackendApiService.cs` — `summary!.BanStatus` (CS8602: summary non-null when user non-null)
- `ViewModels/SettingsViewModel.cs` — `registry.Packages ?? []` (CS8602)
- `Util/RichMessageParser.cs` — `userNames` param type changed to `IReadOnlyDictionary<string, string?>?`; null guard on display name (CS8620)
- `ViewModels/RoomViewModel.cs` — `(msg.Entries ?? [])` for two foreach/FirstOrDefault calls (CS8604)
- `ViewModels/QueueViewModel.cs` — `msg.Modes!.Any(...)` (CS8604: Modes non-null when InQueue)
- `Preview/PreviewStubs.cs` — `#pragma warning disable/restore CS0067` around stub events

---

## Previous Focus

**Issue #28: Windows Defender prompt shows on each launch**

Root cause: `needDefenderModal` was computed as `settings.DefenderExclusionPath != gameDir` — a path-string comparison that fails for some users (casing/trailing-slash differences), causing the prompt to reappear.

Fix: Added `DefenderPromptAnswered` boolean to `LauncherSettings`. The guard is now `!settings.DefenderPromptAnswered && settings.DefenderExclusionPath == null`. The second condition handles backwards compatibility: users whose settings already have `DefenderExclusionPath` set (from the old code) won't see the prompt again. New users see it once; when they respond (accept or skip), both `DefenderPromptAnswered = true` and `DefenderExclusionPath = gameDir` are saved.

Files changed:
- `Models/LauncherSettings.cs` — added `bool DefenderPromptAnswered`
- `ViewModels/MainWindowViewModel.cs` — updated guard and `OnDefenderDecisionMade` callback

---

## Previous Focus

**Registry-based multi-package download system.**

Replaced the single `manifest.json` with a registry-of-packages model:

- `GET /files/registry.json` → `ContentRegistry` (list of packages with `id`, `folder`, `name`, `optional`)
- Each package's manifest at `/files/{folder}/manifest.json`
- Download URL per file: `/files/{folder}/{file.Path}`
- Required packages always downloaded; optional DLC user-selectable

Key new files:
- `Models/ContentRegistry.cs` — `ContentRegistry` + `ContentPackage`
- `Services/IContentRegistryService.cs` + `ContentRegistryService.cs` — singleton with in-memory cache
- `ViewModels/DlcPackageItem.cs` — observable checkbox item for DLC UI

DLC selection flows:
1. **Fresh install**: After folder pick in `SelectGameView`, DLC selector panel appears (required = checked+disabled, optional = toggleable). Choice saved to `LauncherSettings.SelectedDlcIds`.
2. **Settings**: Launcher tab shows available DLC. Checking a new optional DLC saves to settings and immediately triggers re-verification.
3. **Subsequent launches**: `GameDownloadViewModel` uses `SelectedDlcIds` silently to pick packages.

---

All major features are complete. Known technical debt identified but deferred post-release:
- `QueueSocketService.Dispose()` blocks UI thread up to 2s (`Task.Run+.Wait`)
- Chat thread ID hardcoded in `ChatViewModel` (`"17aa3530-d152-462e-a032-909ae69019ed"`)
- `BackendApiService` base URL not configurable via env var (unlike socket service)
- Silent settings corruption fallback in `SettingsStorage.Load()`
- HWID collected by `HardwareInfoService` but not yet sent to backend

---

## Previous Focus

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
