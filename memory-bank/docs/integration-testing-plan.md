# Integration Testing Plan

## Current State

**~12 test files, 207+ unit tests, all passing (xUnit).**

Tests cover isolated pure-logic layers: config parsing, cvar mapping, manifest diffing, state machine, chat grouping, plus Steam state-transition and `AuthCoordinator` integration tests. No UI tests, no ViewModel tests, no socket integration tests.

CI (`build.yml`) runs `dotnet test` and blocks releases on failure.

**NSubstitute is installed** (`d2c-launcher.Tests.csproj`).
**`ISteamManager` is extracted** — `SteamManager` implements it; `FakeSteamManager` is the hand-written fake.
**`FakeSteamManager` is done** (`Fakes/FakeSteamManager.cs`) — 12 state-transition + AuthCoordinator integration tests in `SteamStateTransitionTests.cs` and `AuthCoordinatorTests.cs`.

---

## What Has Interfaces (mockable today)

| Interface | Purpose |
|-----------|---------|
| `IQueueSocketService` | Socket.IO real-time events |
| `IBackendApiService` | REST API |
| `ISettingsStorage` | Launcher settings persistence |
| `IGameLaunchSettingsStorage` | Game-specific settings |
| `ISteamAuthApi` | Steam ticket exchange with backend |
| `ICvarSettingsProvider` | Game cvar read/write |
| `IVideoSettingsProvider` | Video settings |
| `IContentRegistryService` | Game content registry |
| `IHttpImageService` | Image loading |
| `IEmoticonService` | Emoticon cache |
| `IWindowService` | Window management |
| `ILocalManifestService` | Local file manifest |
| `IManifestDiffService` | Manifest diffing (already tested) |
| `IGameDownloadService` | Game HTTP download |

## What Lacks Interfaces (blockers)

| Class | What it needs | Effort |
|-------|--------------|--------|
| `SteamManager` | ✅ Done — `ISteamManager` extracted, `FakeSteamManager` in tests | — |
| `UpdateService` | Extract `IUpdateService` | Low |
| `RedistInstallService` | Extract `IRedistInstallService` | Low |
| `AuthCoordinator` | Already concrete orchestrator; low test value | — |

---

## Barriers to ViewModel Testing

1. **`SteamManager` has no interface** — injected directly into `MainWindowViewModel` and `MainLauncherViewModel`.
2. **`Dispatcher.UIThread` calls embedded in ViewModels** — need Avalonia headless mode or a dispatcher abstraction.
3. **Child ViewModels created with `new` in `MainLauncherViewModel`** — `Queue`, `Party`, `Room`, `NotificationArea`, `Settings`, `Profile`, `Chat` are all constructed inline, not via DI/factory. This prevents swapping them in tests.
4. **Hardcoded API URLs** — `BackendApiService` has `https://api.dotaclassic.ru/` hardcoded (unlike socket service which supports `D2C_SOCKET_URL` env var). Needed for a fake HTTP backend.

---

## Recommended Approach (Layered)

### Layer 1 — Service integration tests (no UI, no Steam) — **Lowest effort, highest value**

- Add **NSubstitute** NuGet package (simpler API than Moq)
- Write a `FakeQueueSocketService` that implements `IQueueSocketService` with helper methods to fire events manually (simulates server pushes)
- Write a `FakeBackendApiService` returning canned DTOs
- Test flows: auth → queue state changes, party invite → accept, socket reconnect behavior
- No Avalonia, no Steam, no HTTP needed

### Layer 2 — ViewModel integration tests (no UI, no Steam)

1. Extract `ISteamManager` from `SteamManager`
2. Add **`Avalonia.Headless.XUnit`** — official package for ViewModel/binding tests without a display
3. Wire `MainWindowViewModel` / `MainLauncherViewModel` with all fake services
4. Drive events on fakes, assert ViewModel state changes

### Layer 3 — Full stack (optional, high complexity)

- Use **WireMock.NET** for a real HTTP mock server (replaces `BackendApiService`)
- Use a **Node.js Socket.IO mock server** or in-process fake for real websocket events
- Only worth it for high-risk flows: queue enter/leave, ready check

---

## Concrete Next Steps

1. ~~**Add NSubstitute**~~ ✅ Done
2. ~~**Extract `ISteamManager`** + write `FakeSteamManager`~~ ✅ Done
3. **Add `D2C_API_URL` env var** to `BackendApiService` (same pattern as existing `D2C_SOCKET_URL` in `QueueSocketService`)
4. **Add `Avalonia.Headless.XUnit`** for ViewModel tests
5. **Write `FakeQueueSocketService`** — implement `IQueueSocketService`, add `SimulateXxx()` helper methods that fire events — reusable across all socket-dependent tests

---

## Test Project Structure Note

The test project (`d2c-launcher.Tests.csproj`) links source files from the main project via `<Compile Include>` with `Link` attributes rather than referencing the main `.csproj`. This avoids pulling in Avalonia, Steamworks.NET, and other platform-heavy assemblies. This pattern should be preserved for pure unit tests.

For ViewModel/integration tests that need Avalonia, a separate test project referencing the main `.csproj` with `Avalonia.Headless.XUnit` may be cleaner than mixing them.
