# Source Engine Launch Mechanisms

When launching a Source 1 game (like Dotaclassic), there are two distinct mechanisms for passing startup configuration to the process.

## `-flag` — Engine Launch Arguments

Parsed by the engine executable **before** the game initializes. Control low-level engine behavior that cannot be changed at runtime.

**Examples:**
```
-novid          skip intro videos
-console        enable developer console
-fullscreen     force fullscreen mode
-windowed       force windowed mode
-w 1920 -h 1080 set resolution
-dedicated      run as dedicated server
-game <dir>     which game mod directory to load
```

These are passed directly to the process via `ProcessStartInfo.Arguments`.

## `+command` — Console Commands at Startup

Prefixed with `+`, these are **console commands** executed by the game's scripting system **after** the engine and game are fully initialized. Equivalent to typing the command in the in-game developer console.

**Examples:**
```
+exec autoconfig.cfg     run a config file on startup
+map <mapname>           load a map immediately
+sv_cheats 1             set a cvar
+connect <ip>            connect to a server
```

`+exec <file>.cfg` loads a text file from the game's `cfg/` directory and executes each line as a console command (`bind`, cvar assignments, etc.).

## Key Differences

| Aspect         | `-flag`               | `+command`            |
|----------------|-----------------------|-----------------------|
| Parsed by      | Engine / OS launcher  | Game console system   |
| Timing         | Before game init      | After game init       |
| Purpose        | Engine / process config | Game / gameplay config |
| Can set cvars  | No                    | Yes                   |
| File-based     | No                    | Yes (`+exec file.cfg`) |

## Usage in D2C Launcher

Both are passed as a single argument string to `ProcessStartInfo.Arguments`:

```csharp
"-novid -console +exec autoconfig.cfg +connect 192.168.1.1"
```

The launcher builds this string in `GameLaunchViewModel`. Runtime console commands (after the game is running) are sent via P/Invoke (`FindWindowA` / `SendMessageA`) in `DotaConsoleConnector`.

---

## How to Add a New Setting

Settings flow: `Models/GameLaunchSettings.cs` → `Services/CfgGenerator.cs` → `launch_settings.json` at runtime.

### Adding a CLI flag (e.g. `-windowed`)

**1. Add a property to `GameLaunchSettings`:**
```csharp
public bool Windowed { get; set; } = false;
```

**2. Emit the flag in `CfgGenerator.BuildCliArgs()`:**
```csharp
if (settings.Windowed)
    parts.Add("-windowed");
```

That's it. The flag will be included in `ProcessStartInfo.Arguments` on next launch.

---

### Adding a game cvar (e.g. `fps_max`)

Game cvars (settings that become console commands) do **not** go through `GameLaunchSettings`. They go through the cvar system:

1. Add property to `Models/CvarSettings.cs`
2. Add `CvarEntry` to `Services/CvarMapping.cs`
3. Add property to `ViewModels/GameSettingsViewModel.cs`

See `memory-bank/docs/settings-architecture.md` for the full flow.

---

### File locations (launch flags only)

| File | Purpose |
|------|---------|
| `Models/GameLaunchSettings.cs` | Add new CLI flag properties here |
| `Services/CfgGenerator.cs` | Map properties → CLI flags |
| `Services/IGameLaunchSettingsStorage.cs` | Interface (rarely needs changes) |
| `Services/GameLaunchSettingsStorage.cs` | Persistence — auto-handles new properties via JSON |
| `%AppData%\d2c-launcher\launch_settings.json` | Runtime storage (auto-created, JSON) |
| `{GameDir}/dota/cfg/d2c_launch.cfg` | Generated on each launch only if `CustomCfgLines` is set |
