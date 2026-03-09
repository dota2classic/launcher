# D2C Launcher — Agent Instructions

## Memory Bank

At the start of **every session**, read all files in `memory-bank/`:

| File | What it covers |
|------|---------------|
| [`memory-bank/projectbrief.md`](memory-bank/projectbrief.md) | Project identity and scope |
| [`memory-bank/productContext.md`](memory-bank/productContext.md) | Why it exists, user journey, UX goals |
| [`memory-bank/systemPatterns.md`](memory-bank/systemPatterns.md) | Architecture and coding patterns |
| [`memory-bank/techContext.md`](memory-bank/techContext.md) | Tech stack, tools, build commands |
| [`memory-bank/activeContext.md`](memory-bank/activeContext.md) | Current focus and recent changes |
| [`memory-bank/progress.md`](memory-bank/progress.md) | Feature status and known gaps |

As you work:
- Update `memory-bank/activeContext.md` when focus shifts or significant progress is made
- Update `memory-bank/progress.md` when features complete or new issues are found
- Add new files to `docs/` when you discover domain knowledge, and add an entry to the table below

---

## Project Overview

**D2C Launcher** is a Windows desktop application for **Dota 2 Classic** — a community-maintained server running the old Dota 2 on the Source 1 engine. The launcher handles:

- Steam authentication (via SteamBridge subprocess)
- Matchmaking queue and real-time updates (Socket.IO)
- Party management (invite, accept, leave)
- Game install validation and launching
- Application auto-updates (Velopack + GitHub Releases)

**Backend API:** `https://api.dotaclassic.ru`

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Language | C# (.NET 10.0) |
| UI Framework | Avalonia UI 11.x (Fluent theme, dark) |
| UI Pattern | MVVM via `CommunityToolkit.Mvvm` |
| DI Container | `Microsoft.Extensions.DependencyInjection` |
| Real-time | `SocketIOClient` (Socket.IO / WebSocket) |
| Steam | `Steamworks.NET` + custom `SteamBridge` subprocess |
| API Client | NSwag-generated from `api-openapi.json` |
| Audio | `NAudio` |
| Updates | `Velopack` |
| Dialogs | `DialogHost.Avalonia` |
| Icons | `Material.Icons.Avalonia` |

---

## Key Directories

- `Views/` — Avalonia XAML UI; `Components/` for reusable sub-views
- `ViewModels/` — MVVM ViewModels (CommunityToolkit)
- `Services/` — Business logic: REST API, WebSocket, Steam auth, settings, updates
- `Models/` — DTOs and domain models
- `Integration/` — Steam process monitoring (`SteamManager`), Dota console commands
- `Util/` — Logging, audio, P/Invoke, XAML converters
- `Generated/` — NSwag-generated API client (**do not edit**)
- `SteamBridge/` — Separate console app that queries Steam SDK and outputs JSON to stdout
- `tools/mockups/` — HTML reference mockups for Avalonia UI design (rendered via `screenshot-html.ps1`)

---

## Architecture

See [`memory-bank/systemPatterns.md`](memory-bank/systemPatterns.md) for all architecture patterns (state routing, MVVM, SteamBridge, real-time, settings sync, game launch, API client).

---

## Build & Run

### Prerequisites
- .NET 10.0 SDK
- Steam must be running for testing Steam auth features
- The launcher uses Steam App ID **480** (Spacewar/demo). Dota 2 itself ships its own `steam_appid.txt` — do not add or override it for the game directory

### Build
```bash
dotnet build
```

### Run (debug)
```bash
dotnet run --project d2c-launcher
```

### Publish (production, win-x64)
```bash
dotnet publish d2c-launcher -c Release -r win-x64 --self-contained
dotnet publish SteamBridge -c Release -r win-x64 --self-contained -p:PublishTrimmed=true
```

### Release
Push a tag matching `v*.*.*` to trigger GitHub Actions. The workflow:
1. Builds and publishes both projects
2. Packs with Velopack
3. Creates a GitHub Release with the artifacts

---

## Localization
- UI text is in **Russian** (Cyrillic)
- No external localization framework — strings are hardcoded in XAML and ViewModels
- When adding new UI text, write in Russian

---

## Important Files

| File | Purpose |
|------|---------|
| [App.axaml.cs](App.axaml.cs) | DI registration, app startup |
| [Integration/SteamManager.cs](Integration/SteamManager.cs) | Steam process monitoring, auth tickets |
| [Services/QueueSocketService.cs](Services/QueueSocketService.cs) | Socket.IO real-time events |
| [Services/BackendApiService.cs](Services/BackendApiService.cs) | REST API calls |
| [ViewModels/MainWindowViewModel.cs](ViewModels/MainWindowViewModel.cs) | App state routing |
| [ViewModels/MainLauncherViewModel.cs](ViewModels/MainLauncherViewModel.cs) | Main screen logic |
| [Generated/DotaclassicApiClient.g.cs](Generated/DotaclassicApiClient.g.cs) | **Auto-generated — do not edit** |
| [SteamBridge/Program.cs](SteamBridge/Program.cs) | Steam SDK subprocess |
| [api-openapi.json](api-openapi.json) | Backend OpenAPI spec |

---

## Developer Tools

### Component Preview (Storybook-like)

Render a single UI component in isolation, screenshot it, and iterate — without running the full launcher.

```powershell
# From repo root (use powershell, not pwsh — pwsh is not installed):
powershell -ExecutionPolicy Bypass -File tools/preview.ps1 PartyPanel
# → C:\...\tools\screenshots\20260302_143201.png
```

The script always runs an incremental `dotnet build`, launches the app in `--preview` mode, waits for the window, screenshots it, kills the process, and prints the PNG path.

Use the `Read` tool on the returned path to view the screenshot.

**Available components:**

| Name | View | ViewModel |
|------|------|-----------|
| `PartyPanel` | Party member list with invite button | `PartyViewModel` (stub) |
| `QueueButton` | Matchmaking search button | `QueueViewModel` (stub) |
| `GameSearchPanel` | Mode checkboxes (shows 3 mock modes) | `QueueViewModel` (stub) |
| `AcceptGameModal` | Ready check accept/decline dialog | `RoomViewModel` (stub) |
| `NotificationArea` | Floating invite notifications | `NotificationAreaViewModel` (stub) |
| `LaunchSteamFirst` | "Launch Steam first" screen | `LaunchSteamFirstViewModel` |
| `SelectGame` | Game directory picker screen | `SelectGameViewModel` |

**To add a new component to the registry:** edit [Preview/PreviewRegistry.cs](Preview/PreviewRegistry.cs) and add an entry.
Stub services are in [Preview/PreviewStubs.cs](Preview/PreviewStubs.cs).

**Note:** Steam does not need to be running for the preview tool to work.

### Full App Screenshot

To screenshot the full running app (all real services, real routing), use:

```powershell
# From repo root (use powershell, not pwsh):
powershell -ExecutionPolicy Bypass -File tools/screenshot.ps1
# → C:\...\tools\screenshots\20260302_143201.png
```

The script builds incrementally, launches the app without any special flags, waits up to 15 seconds for the window, screenshots it, kills the process, and prints the PNG path.

Use the `Read` tool on the returned path to view the screenshot.

**Notes:**
- If Steam is not running the app will show the `LaunchSteamFirst` screen — that is expected and still useful for layout review.
- Pass `-WaitSeconds 20` if startup is slow.
- Output goes to the same `tools/screenshots/` directory as component previews.

### HTML Screenshot

To render an arbitrary HTML file and screenshot it (useful for mockups and design iteration):

```powershell
# From repo root (use powershell, not pwsh):
powershell -ExecutionPolicy Bypass -File tools/screenshot-html.ps1 tools/mockups/d2c-launcher-views.html
powershell -ExecutionPolicy Bypass -File tools/screenshot-html.ps1 tools/mockups/some-mockup.html -Width 1200 -Height 900
# → C:\...\tools\screenshots\20260302_143201.png
```

Uses headless Chrome (`--headless`) — no window management, no Steam required. Width/Height default to 1000×800.

HTML mockups live in `tools/mockups/` — reference designs for building Avalonia UI, rendered with this tool.

---

## Documentation

Project documentation lives in the `docs/` directory. Current files:

| File | Topic |
|------|-------|
| [docs/source-engine-launch.md](docs/source-engine-launch.md) | Source 1 engine launch mechanics (`-flag` vs `+command`), usage in D2C Launcher |
| [docs/source-engine-config-persistence.md](docs/source-engine-config-persistence.md) | `config.cfg` format, `FCVAR_ARCHIVE`, `host_writeconfig`, reading config from launcher |
| [docs/settings-architecture.md](docs/settings-architecture.md) | Settings system: CvarMapping, CompositeCvarMapping, BindMapping, SettingsViewModel, adding new settings |
| [docs/game-update-manifest.md](docs/game-update-manifest.md) | Game update manifest format, `exact`/`existing` modes, `LocalManifestService`, `ManifestDiffService`, update flow |
| [docs/client-dll-patching.md](docs/client-dll-patching.md) | Binary patching of `client.dll`: FCVAR_CHEAT removal, default value patch, PE layout, sync strategy |

When you discover new domain knowledge, architectural decisions, or non-trivial technical details during implementation, **write them up as a new `.md` file in `docs/`** and add an entry to this table. Keep docs focused — one topic per file.

---

## Do Not

- Edit `Generated/DotaclassicApiClient.g.cs` manually — regenerate from the OpenAPI spec
- Add platform-specific code for non-Windows targets — this is Windows-only
- Commit secrets or tokens
- Use `Task.Result` or `.Wait()` — always `await` async methods
- Register new ViewModels as singletons — they should be transient or manually created
