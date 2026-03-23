# Active Context

## Current State

All major features are shipped. The launcher is in maintenance/polish mode. Working on #135 (replace static CvarMapping and DotaConsoleConnector with injectable services).

---

## Recently Completed (last few sessions)

| Issue | What was done |
|-------|--------------|
| #133 | Extracted `IUserNameResolver`, `IEmoticonSnapshotBuilder`, `IChatMessageStream`/`IChatMessageStreamFactory` from `ChatViewModel` (550→~230 lines); all three registered as singletons; `ChatViewModelFactory` updated; preview stubs updated |
| #135 | Extracted `ICvarRegistry`, `ICvarFileService`, `IGameWindowService` — injectable wrappers around static `CvarMapping`, `DotaCfgReader`/`DotaCfgWriter`, `DotaConsoleConnector`; `CvarSettingsProvider` and `GameLaunchViewModel` now use interfaces |
| #106 | Skip connect if already on target server — `IsAlreadyConnectedToAsync` in `GameLaunchViewModel`: sends `status` via NetCon, parses port from `type(dedicated)` line, returns early if port matches `ServerUrl`; PR #129 |
| #120 | Migrated game console interaction from WM_COPYDATA to NetCon: `INetConService`/`NetConService` singleton; lifecycle managed in `GameLaunchViewModel` via `RefreshRunState` transitions; `PushCvarIfGameRunning` and `ConnectToGameAsync` now use `SendCommandAsync`; `DotaConsoleConnector` kept only for window operations |
| #117 | Bot game progress on human mode lock — new `botGameProgress` field (0–1) from backend shown as X% in restriction text on locked human game mode cards; also refreshed OpenAPI spec + regenerated client; PR #119 |
| #112 | Emoticon picker flyout on chat input emoji button — inserts `:code:` at caret position; `InputEmoticonPicker` in `ChatViewModel`; `OnInputEmoticonClicked` in code-behind handles caret-aware insertion |
| #109 | Trivia (Shopkeeper's Quiz style) while searching — TriviaViewModel, ITriviaRepository/LocalJsonTriviaRepository, TriviaPanel; shows in GameSearchPanel replacing mode list when IsSearching; item recipe + multiple choice types; 20s timer, 3 guesses, cumulative score |
| #104 | Design standardisation: added `PrimaryButton`, `DangerButton`, `ToastDismissButton` global styles; `FontSize2XS=9`, `FontSize2XL5=20` tokens; replaced all hardcoded font sizes in LivePanel, ProfilePanel, LauncherHeader with tokens; unified button colors (red → `#c23c2a`, blue → `#1a5aaa`→`#3a90d6` gradient) across AcceptGameModal, MainLauncherView, NotificationArea, LauncherHeader |
| #102 | Dotaclassic Plus badge in chat is now a clickable Button; opens `https://dotaclassic.ru/store` via `OnDotaclassicPlusClicked` in `ChatPanel.axaml.cs` |
| #98 | Player role icons in chat message headers — shield for moderator (bronze) / admin (grey), custom image or star for OLD subscriber; data flows from `UserDTO.Roles`/`Icon`/`Title` → `ChatMessageData` → `ChatMessageView` → `ChatPanel.axaml` |
| #97 | Chat react hover toolbar + picker — `ChatQuickReactViewModel`; `EmoticonData` now has `Id`; `IEmoticonService.LoadEmoticonsAsync()` returns images + ordered list; hover toolbar shows top-3 + flyout picker |
| #96 | Chat reactions — `ChatReactionViewModel`, `ChatReactionData`, reaction pills in `ChatPanel.axaml`; SSE uses `ThreadMessageDTO` deserialization |
| #95 | i18n achievement notifications — `ru.json` strings for all achievement keys; `AchievementToastViewModel` looks up title via `I18n.T()` |
| #94 | JSON-based i18n system — `Resources/Locales/ru.json`, `I18n.cs`, `{l:T}` XAML extension; `Strings.cs` now delegates to `I18n.T()` |
| #92 | Achievement toast notifications — `AchievementToastViewModel`, `NotificationCreated` socket event routing, local asset images |
| #93 | +connect on launch — `LaunchGame($"+connect {url}")` when game not running |
| #88 | SettingsViewModel split — `GameSettingsViewModel`, `LauncherPrefsViewModel`, `DlcViewModel`; `SettingsPanel` is now a tab shell |
| #90 | FakeSteamManager + integration tests — 12 state-transition and AuthCoordinator tests |
| #80 | Live matches tab — `LiveViewModel`, `LivePanel`, minimap with animated hero positions |
| #23 | Abandon game — red X button + confirm overlay; excludes unranked/highroom modes |

---

## Next Steps / Open Issues

| Issue | Title |
|-------|-------|
| #22 | Steam not being detected — root cause unknown |
| #21 | Setting autorepeat doesn't work — may not be a cvar bug |
| #18 | Parallelize local file scan + remote manifest load |
| #13 | Support chat scrolling |
| #8 | Research crash dump analysis |
| #7 | Handling game crashes |

---

## Known Technical Debt

| Item | Notes |
|------|-------|
| `FakeQueueSocketService` | Planned for integration tests; `FakeSteamManager` is done |
| `QueueSocketService.Dispose()` blocks UI | `Task.Run+.Wait` up to 2s |
| Chat thread ID hardcoded | `"17aa3530-d152-462e-a032-909ae69019ed"` in `ChatViewModel` |
| Keybind settings UI | `config.cfg` bind lines parsed but not exposed in UI |

---

## Active Decisions / Patterns to Remember

- **Localization:** Never hardcode Russian strings. Use `I18n.T("section.key")` / `{l:T 'key'}`. `Strings.cs` is legacy — do not add entries.
- **Settings sub-VMs:** Gameplay/video cvars → `GameSettingsViewModel`; launcher prefs → `LauncherPrefsViewModel`. `SettingsViewModel` is a thin container.
- **Game mode default:** Mode ID 7 (Bots). Mode 12 no longer exists.
- **Preview tool:** Run `powershell -ExecutionPolicy Bypass -File tools/preview.ps1 <Name>` — always use `Read` on the screenshot to verify before declaring done.
