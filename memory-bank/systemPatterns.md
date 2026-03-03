# System Patterns

## App State Routing

`MainWindowViewModel` is the root. It observes Steam and settings state and swaps the window content:

```
Steam not running / not logged in  →  LaunchSteamFirstView
Game directory not set             →  SelectGameView
Both conditions met                →  MainLauncherView
```

Child ViewModels are created fresh each time a state is entered — they are not singletons.

## MVVM Conventions

- `[ObservableProperty]` for all bindable properties (CommunityToolkit.Mvvm source generator)
- `[RelayCommand]` for all command methods
- `partial class` required for source generators to work
- Services injected via constructor DI; ViewModels receive what they need
- Never register ViewModels as singletons — instantiate them directly when entering a state

```csharp
// Good
public partial class MyViewModel : ObservableObject
{
    [ObservableProperty] private string _status = "";
    [RelayCommand] private async Task DoSomethingAsync() { ... }
}
```

## Service Registration

All services are registered as singletons in `App.axaml.cs`:

```csharp
services.AddSingleton<IMyService, MyService>();
```

ViewModels are resolved manually (not from the container) when a state transition occurs. This prevents stale state from lingering in long-lived ViewModels.

## SteamBridge Pattern

The main app cannot use Steamworks.NET directly when published with `PublishTrimmed=true` (IL linking breaks native interop). Solution: subprocess isolation.

**Flow:**
1. `SteamManager` spawns `d2c-steam-bridge.exe` as a child process
2. SteamBridge queries Steam SDK, writes a single JSON snapshot to stdout, exits
3. `SteamManager` reads stdout, deserializes the snapshot
4. Snapshot contains: SteamID, display name, avatar (RGBA bytes), auth ticket (hex)

**Retry:** 12-second timeout per attempt; exponential backoff on failure. Retries until Steam responds or user cancels.

**Why stdout, not IPC:** Simplest cross-process protocol for a single-response request. No named pipes, no sockets, no shared memory needed.

## Real-time Communication

`QueueSocketService` wraps a `SocketIOClient` connection to `wss://api.dotaclassic.ru`.

**Inbound events (server → client):**

| Event | Meaning |
|-------|---------|
| `QueueStateUpdated` | Queue position / status changed |
| `PlayerRoomFound` | Match found, transition to ready check |
| `PlayerRoomStateUpdated` | Ready check state changed |
| `PlayerGameStateUpdated` | Game server state (starting, started) |
| `PartyInviteReceived` | Another player invited the user |
| `PartyUpdated` | Party composition changed |
| `NotificationCreated` | Generic notification |
| `ServerSearchingUpdated` | Server search status |

**Outbound events (client → server):**

| Event | Meaning |
|-------|---------|
| `EnterQueueAsync` | Join matchmaking queue |
| `LeaveAllQueuesAsync` | Leave all active queues |
| `SetReadyCheckAsync` | Accept or decline ready check |
| `InviteToPartyAsync` | Invite a player by SteamID |
| `AcceptPartyInviteAsync` | Accept a pending party invite |

## Settings Persistence

Two separate stores — never merge them:

| Store | Path | Contents |
|-------|------|---------|
| Launcher JSON | `%AppData%\d2c-launcher\launcher_settings.json` | `GameDirectory`, `BackendAccessToken`, launch flags (NoVid, Language) |
| Game config | `{GameDir}/dota/cfg/config.cfg` | All Source engine cvars (fps_max, sensitivity, etc.) |

The launcher reads `config.cfg` on startup and on game exit. It writes to `config.cfg` directly (via `DotaCfgWriter`) when the user changes a cvar while the game is not running. When the game is running, changes are pushed live via `DotaConsoleConnector` and the game flushes `config.cfg` on clean exit.

→ See `docs/settings-architecture.md` for the full data flow and adding new settings.

## Two-Phase Config Sync

The Source engine's `WM_COPYDATA` handler queues commands in `Cbuf` — it does not execute them synchronously. So you cannot write `host_writeconfig` and immediately read the file.

**Pattern (runs every ~7.5 seconds while game is open):**
1. **Tick N:** Send `host_writeconfig` via `DotaConsoleConnector.SendCommand()`
2. **Tick N+1 (~1.5s later):** Read and parse `config.cfg`

On game exit (clean shutdown): skip phase 1, only phase 2 (engine already flushed the file).

**Feedback loop prevention:** `IsSyncingFromGame = true` while reading to prevent the settings change handlers from pushing values back to the game.

→ See `docs/source-engine-config-persistence.md` for the full details.

## Game Launch

`GameLaunchViewModel` builds the process arguments and starts the game:

```csharp
var args = CfgGenerator.BuildCliArgs(launchSettings); // "-novid -language russian ..."
// + "+exec d2c_launch.cfg" if CustomCfgLines is set
Process.Start(new ProcessStartInfo { FileName = dotaExePath, Arguments = args });
```

Runtime console commands (after launch) are sent via P/Invoke:
- `FindWindowA("Valve001", null)` — find the game window
- `SendMessageA(hwnd, WM_COPYDATA, ...)` — send a console command string

→ See `docs/source-engine-launch.md` for `-flag` vs `+command` semantics.

## API Client Pattern

`Generated/DotaclassicApiClient.g.cs` is auto-generated from `api-openapi.json` using NSwag.

**Never edit it manually.** To regenerate after API changes:
```
nswag run nswag.json
```

`BackendApiService` wraps the generated client and adds auth header injection, error handling, and higher-level methods.

## Async Conventions

- Always `await` — never `.Result` or `.Wait()`
- Async method names end in `Async`
- UI thread marshaling in Avalonia: use `Dispatcher.UIThread.Post()` when updating observable properties from background tasks
