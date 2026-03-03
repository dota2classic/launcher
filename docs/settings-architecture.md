# Settings Architecture

## Overview

The settings system has two separate stores:

1. **Launcher JSON** (`%AppData%\d2c-launcher\launch_settings.json`) — CLI launch flags only (`NoVid`, `Language`, `ExtraArgs`, `CustomCfgLines`)
2. **config.cfg** (`{GameDir}/dota/cfg/config.cfg`) — source of truth for all game cvars, read/written directly by the launcher

There is no periodic sync loop. The launcher reads config.cfg on startup and on game exit, and writes to it directly when the user changes a cvar setting while the game is not running.

## Setting Types

### 1:1 Cvars (`CvarMapping`)

One setting property → one Source engine cvar. Each entry is a `CvarEntry` record operating on `CvarSettings`:

```csharp
new("dota_camera_disable_zoom",
    s => s.DisableCameraZoom ? "1" : "0",    // getter: CvarSettings → string
    (s, v) => s.DisableCameraZoom = v is "1", // setter: string → CvarSettings
    IsEmpty: _ => false),                      // always write to cfg
```

Entries: `fps_max`, `con_enable`, `dota_camera_disable_zoom`, `dota_force_right_click_attack`, `dota_player_auto_repeat_right_mouse`, `dota_reset_camera_on_spawn`.

### Composite Cvars (`CompositeCvarMapping`)

One setting property → multiple cvars. Used when a single user-facing toggle/dropdown controls multiple engine variables.

Example: **AutoAttack mode** (enum) → `dota_player_units_auto_attack` + `dota_player_units_auto_attack_after_spell`.

Each `CompositeCvarEntry` has `GetValues()` returning a `Dictionary<string,string>` and `SetValues()` accepting one.

## Data Flow

### Launcher startup
```
config.cfg ──(DotaCfgReader)──> CvarSettingsProvider (in-memory) ──> SettingsViewModel ──> UI
launch_settings.json ──(GameLaunchSettingsStorage)──> launch flags (NoVid, Language, etc.)
```

### User changes a cvar (game not running)
```
UI ──> SettingsViewModel setter ──> CvarSettingsProvider.Update()
       ──> in-memory update + DotaCfgWriter writes to config.cfg
```

### User changes a cvar (game running)
```
UI ──> SettingsViewModel setter ──> CvarSettingsProvider.Update() (in-memory only, skips config.cfg write)
       ──> PushCvar delegate ──> DotaConsoleConnector.SendCommand("cvar value")
Game flushes to config.cfg on exit.
```

### User changes a launch flag (NoVid, Language)
```
UI ──> SettingsViewModel setter ──> GameLaunchSettingsStorage.Save() ──> JSON file
```

### Game launch
```
CfgGenerator.BuildCliArgs(launchSettings) → "-novid -language russian ..."
CfgGenerator.Generate(launchSettings, gameDir) → "+exec d2c_launch.cfg" (only if CustomCfgLines set)
dota.exe started with these arguments
```

### Game exit
```
GameLaunchViewModel detects OurGameRunning → None
  ──> CvarSettingsProvider.LoadFromConfigCfg(gameDir)
       ──> DotaCfgReader reads config.cfg
       ──> updates in-memory CvarSettings
       ──> fires CvarChanged
            ──> SettingsViewModel.RefreshFromCvarProvider()
```

## Adding a New Setting

### Simple bool cvar

1. Add property to `Models/CvarSettings.cs`
2. Add `CvarEntry` to `CvarMapping.Entries`
3. Add property to `SettingsViewModel.cs` (getter/setter using `_cvarProvider`, plus `PushCvar` call)
4. Add toggle to `Views/Components/SettingsPanel.axaml`
5. Add property name to `SettingsViewModel.RefreshFromCvarProvider()`

### Multi-cvar toggle

1. Add property to `Models/CvarSettings.cs`
2. Add `CompositeCvarEntry` to `CompositeCvarMapping.Entries`
3. Add property to `SettingsViewModel.cs` — in the setter, push each constituent cvar individually
4. Add toggle to `Views/Components/SettingsPanel.axaml`

### Launch flag (CLI argument)

1. Add property to `Models/GameLaunchSettings.cs`
2. Add to `CfgGenerator.BuildCliArgs()`
3. Add property to `SettingsViewModel.cs` (getter/setter using `_launchStorage`)
4. Add UI control to `Views/Components/SettingsPanel.axaml`

## Key Files

| File | Purpose |
|------|---------|
| `Models/CvarSettings.cs` | Cvar data model (in-memory, backed by config.cfg) |
| `Models/GameLaunchSettings.cs` | Launch flags (persisted as JSON) |
| `Models/AutoAttackMode.cs` | Enum for auto-attack states |
| `Services/ICvarSettingsProvider.cs` | Interface for cvar state management |
| `Services/CvarSettingsProvider.cs` | Singleton: in-memory cvar state, reads/writes config.cfg |
| `Services/CvarMapping.cs` | 1:1 cvar ↔ property registry |
| `Services/CompositeCvarMapping.cs` | Multi-cvar ↔ property registry |
| `Services/DotaCfgReader.cs` | Reads config.cfg |
| `Services/DotaCfgWriter.cs` | Writes cvars into config.cfg |
| `Services/CfgGenerator.cs` | Writes d2c_launch.cfg (CustomCfgLines only) + builds CLI args |
| `ViewModels/SettingsViewModel.cs` | All settings UI properties + live push |
| `Views/Components/SettingsPanel.axaml` | Settings UI layout |
