# Source Engine Config Persistence

How the Source 1 engine saves and loads player configuration (cvars, keybinds).

---

## config.cfg

The primary config file lives at `{GameDir}/dota/cfg/config.cfg`. It contains all cvars marked with `FCVAR_ARCHIVE` and all keybinds.

### Format

```
unbindall
bind "a" "mc_attack"
bind "s" "dota_stop"
dota_camera_speed "3000"
sensitivity "3"
volume "0.077368"
```

Each line is either:
- `bind "key" "command"` — a key binding
- `cvar_name "value"` — an archived cvar with its current value

### When the Engine Writes config.cfg

The engine writes `config.cfg` automatically on **clean shutdown** (normal game exit). It does **not** write on crash or force-kill.

### Forcing a Write: `host_writeconfig`

The console command `host_writeconfig` forces an immediate flush of all archived cvars to disk:

```
host_writeconfig                  // writes to cfg/config.cfg (default)
host_writeconfig <filename>       // writes to cfg/<filename>.cfg
```

This is the same operation the engine performs on shutdown, but triggered on demand.

### Settings Menu Behavior

The in-game settings UI (options menu) does **not** apply changes to cvars in real time. Changes are held in the UI until the user **exits the settings screen** (clicks OK / closes the menu). Only then are the cvar values actually updated in memory. This means:

- `host_writeconfig` while the settings screen is open will **not** capture pending UI changes
- Changes become visible to `host_writeconfig` only after the settings menu is closed
- This is an engine/UI limitation — there is no way to read uncommitted settings screen state

### When config.cfg Is Read

On game startup, the engine executes `config.cfg` automatically, restoring all saved settings. This happens after `default.cfg` but before `autoexec.cfg`.

**Startup execution order:**
1. `default.cfg` (engine defaults)
2. `config.cfg` (saved user settings)
3. `autoexec.cfg` (user overrides, if present)
4. Any `+exec <file>.cfg` from launch arguments

---

## FCVAR_ARCHIVE

`FCVAR_ARCHIVE` is an engine-level flag on cvar definitions. Only cvars with this flag are persisted to `config.cfg`. Not all cvars have it — debug/cheat cvars typically do not.

When you set an archived cvar via console (e.g. `sensitivity 3`), the new value is held in memory and written to `config.cfg` on the next flush (shutdown or `host_writeconfig`).

---

## Launcher ↔ Game Config Sync

The launcher synchronizes settings bi-directionally with the game:

### Launcher → Game (on launch)

1. `CfgGenerator` writes `d2c_launch.cfg` from `GameLaunchSettings`
2. Game is launched with `+exec d2c_launch.cfg` in arguments
3. Engine executes the cfg after startup, applying the launcher's settings

### Game → Launcher (while running)

The game only writes `config.cfg` on **clean shutdown**. To pick up mid-game changes (e.g. user changed sensitivity in the Dota settings menu), the launcher uses a **two-phase sync**:

1. **Phase 1 (tick N):** Send `host_writeconfig` via `DotaConsoleConnector` (`WM_COPYDATA`). This tells the engine to flush all `FCVAR_ARCHIVE` cvars to `config.cfg`.
2. **Phase 2 (tick N+1, ~1.5s later):** Read and parse `config.cfg`. The delay is needed because `WM_COPYDATA` buffers the command for the engine's next frame — the file isn't written by the time `SendMessageA` returns.

This runs every ~7.5 seconds (5 timer ticks at 1.5s/tick). On game exit, only the read phase runs (clean shutdown already flushed the file).

### Why Two Phases?

Source engine's `WM_COPYDATA` handler adds the command to `Cbuf` (the command buffer). The command executes on the next engine frame, not inline during the Windows message handler. So:

```
SendMessageA(WM_COPYDATA, "host_writeconfig")  →  returns immediately
   ↓
Engine main loop: Cbuf_Execute()  →  host_writeconfig runs  →  file written
   ↓
Next launcher tick: read config.cfg  →  sees fresh data
```

### Feedback Loop Prevention

When the launcher reads new values from `config.cfg` and saves them, `IsSyncingFromGame` is set to `true`. `MainLauncherViewModel` checks this flag to avoid pushing the same values back to the game via settings change handlers.

### Parsing Notes

- Lines starting with `bind` are keybinds
- All other `name "value"` lines are cvar assignments
- Values are always double-quoted strings (even numeric ones)
- The file starts with `unbindall` to clear previous binds before applying
- Empty lines or `//` comments may appear but are rare in engine-generated files
- Only cvars registered in `CvarMapping.Entries` are synced — unknown cvars are ignored

---

## Related Files

| File | Purpose |
|------|---------|
| `{GameDir}/dota/cfg/config.cfg` | Engine-managed, auto-saved on shutdown |
| `{GameDir}/dota/cfg/autoexec.cfg` | User-created, runs after config.cfg on startup |
| `{GameDir}/dota/cfg/d2c_launch.cfg` | Launcher-generated, applied via `+exec` on launch |
| `Services/DotaCfgReader.cs` | Launcher-side parser for config.cfg |
| `Services/CfgGenerator.cs` | Generates d2c_launch.cfg from launcher settings |
| `Services/CvarMapping.cs` | Declarative cvar ↔ property registry |
| `ViewModels/GameLaunchViewModel.cs` | Two-phase sync orchestration (flush + read) |
| `Integration/DotaConsoleConnector.cs` | Sends console commands via WM_COPYDATA |
