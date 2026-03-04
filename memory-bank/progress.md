# Progress

## Feature Status

### Core Infrastructure


| Feature                                    | Status         | Notes                                           |
| ------------------------------------------ | -------------- | ----------------------------------------------- |
| Steam auth via SteamBridge subprocess      | ✅ Done         | 12s timeout, exponential backoff                |
| App state routing (Steam → GameDir → Main) | ✅ Done         | `MainWindowViewModel`                           |
| DI container setup                         | ✅ Done         | `App.axaml.cs`                                  |
| Settings persistence (launcher JSON)       | ✅ Done         | `%AppData%\d2c-launcher\launcher_settings.json` |
| Application auto-updates                   | ✅ Done         | Velopack + GitHub Releases                      |
| Hardware info logging + HWID               | 🔄 In progress | `HardwareInfoService`, not yet committed        |


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


### Testing


| Area                  | Status |
| --------------------- | ------ |
| xUnit project setup   | ✅ Done |
| `CfgGenerator` tests  | ✅ Done |
| `DotaCfgReader` tests | ✅ Done |
| `CvarMapping` tests   | ✅ Done |
| CI test step          | ✅ Done |


### CI/CD


| Step                    | Status |
| ----------------------- | ------ |
| Build workflow          | ✅ Done |
| Test step               | ✅ Done |
| Velopack packaging      | ✅ Done |
| GitHub Release creation | ✅ Done |


---

## Known Gaps / Backlog


| Item                 | Priority | Notes                                                           |
| -------------------- | -------- | --------------------------------------------------------------- |
| Keybind settings UI  | —        | `config.cfg` bind lines are parsed but not exposed in UI        |
| Send HWID to backend | —        | HardwareInfoService in progress; backend endpoint may be needed |
| Extra launch args UI | —        | `ExtraArgs` field exists in model, UI unknown                   |
| Custom cfg lines UI  | —        | `CustomCfgLines` field exists, wired to `d2c_launch.cfg`        |


---

## Architecture / Docs Status


| Doc                                        | Status    |
| ------------------------------------------ | --------- |
| `docs/source-engine-launch.md`             | ✅ Written |
| `docs/source-engine-config-persistence.md` | ✅ Written |
| `docs/settings-architecture.md`            | ✅ Written |
| Memory bank (`memory-bank/`)               | ✅ Written |


