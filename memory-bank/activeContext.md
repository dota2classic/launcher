# Active Context

## Current State

All major features are shipped. The launcher is in maintenance/polish mode. No active in-progress work.

---

## Recently Completed (last few sessions)

| Issue | What was done |
|-------|--------------|
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
