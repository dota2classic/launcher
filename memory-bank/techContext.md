# Tech Context

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
| Hardware info | `System.Management` (WMI) |

## Key Directories

| Directory | Purpose |
|-----------|---------|
| `Views/` | Avalonia XAML views; `Components/` for reusable sub-views |
| `ViewModels/` | MVVM ViewModels (CommunityToolkit) |
| `Services/` | Business logic: REST API, WebSocket, Steam auth, settings, updates |
| `Models/` | DTOs and domain models |
| `Integration/` | Steam process monitoring (`SteamManager`), Dota console commands |
| `Util/` | Logging (`AppLog`), audio, P/Invoke helpers, XAML converters |
| `Generated/` | NSwag-generated API client — **do not edit** |
| `SteamBridge/` | Separate console app that queries Steam SDK and outputs JSON to stdout |
| `Preview/` | Storybook-like component previewer (registry + stubs) |
| `memory-bank/` | Structured project context for AI sessions (6 core files + `docs/` subfolder) |
| `tools/` | PowerShell scripts for preview and screenshot automation |

## Important Files

| File | Purpose |
|------|---------|
| `App.axaml.cs` | DI registration, app startup |
| `Program.cs` | Entry point, Velopack init |
| `Integration/SteamManager.cs` | Steam subprocess monitoring, auth tickets |
| `Services/QueueSocketService.cs` | Socket.IO real-time events |
| `Services/BackendApiService.cs` | REST API calls |
| `Services/HardwareInfoService.cs` | WMI hardware enumeration + HWID |
| `ViewModels/MainWindowViewModel.cs` | App state routing |
| `ViewModels/MainLauncherViewModel.cs` | Main screen logic |
| `ViewModels/GameLaunchViewModel.cs` | Game process lifecycle, config sync |
| `ViewModels/SettingsViewModel.cs` | All settings UI properties + live push |
| `Services/CvarSettingsProvider.cs` | In-memory cvar state, reads/writes config.cfg |
| `Services/CvarMapping.cs` | 1:1 cvar ↔ property registry |
| `Services/CfgGenerator.cs` | Builds CLI args + writes d2c_launch.cfg |
| `Generated/DotaclassicApiClient.g.cs` | **Auto-generated — do not edit** |
| `SteamBridge/Program.cs` | Steam SDK subprocess |
| `api-openapi.json` | Backend OpenAPI spec |
| `nswag.json` | NSwag configuration for code generation |

## Build & Run

### Prerequisites
- .NET 10.0 SDK
- Steam must be running for testing Steam auth features

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

### Regenerate API client
```bash
nswag run nswag.json
```

### Release
Push a tag matching `v*.*.*` → GitHub Actions builds, packs with Velopack, creates a GitHub Release.

## Developer Tools

### Component Preview (no Steam needed)
```powershell
# Use powershell, not pwsh
powershell -ExecutionPolicy Bypass -File tools/preview.ps1 PartyPanel
```
Available components: `PartyPanel`, `QueueButton`, `GameSearchPanel`, `AcceptGameModal`, `NotificationArea`, `LaunchSteamFirst`, `SelectGame`

Add new components in `Preview/PreviewRegistry.cs`. Stub services in `Preview/PreviewStubs.cs`.

### Full App Screenshot
```powershell
powershell -ExecutionPolicy Bypass -File tools/screenshot.ps1
# optional: -WaitSeconds 20
```
Both tools: build incrementally, launch, screenshot, kill, print PNG path. Use `Read` tool to view the image. Delete screenshots after viewing.

Screenshots saved to `tools/screenshots/` (gitignored). Scripts auto-delete previous screenshots on each run.

## Localization
- All UI text is in **Russian** (Cyrillic)
- Uses `I18n` system: `Resources/Locales/ru.json` (embedded), `I18n.T("section.key")` in C#, `{l:T 'section.key'}` in XAML
- `Resources/Strings.cs` is legacy — do not add new entries; use `I18n.T()` directly
- See `memory-bank/docs/localization.md`
