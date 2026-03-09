# Computer-Use Workflow

How to use the `computer-use` MCP tools to implement and visually verify UI changes.

## Screenshot Setup

- Window title fragment: **`dotaclassic`** (matches `dotaclassic v1.x.x`)
- Screenshot tool returns a file path — use `Read` tool on the path to view it as an image
- MCP server: `tools/computer-use-mcp/server.py`

## Example Task: Add New Cvar to Settings Modal

### 1. Understand the domain
- Read `docs/settings-architecture.md` — `CvarMapping`, `CompositeCvarMapping`, `BindMapping` patterns
- Read `docs/source-engine-config-persistence.md` — how cvars are read/written

### 2. Find the right place
- Read `ViewModels/SettingsViewModel.cs` — follow existing cvar mapping patterns
- Read the settings modal XAML in `Views/` — understand UI structure

### 3. Implement
- Add cvar mapping in `SettingsViewModel.cs`
- Add UI control in settings XAML (text in Russian)

### 4. Build
```bash
dotnet build d2c-launcher.csproj 2>&1 | tail -20
```

### 5. Visual verification loop

Each UI iteration: **edit → kill → build → launch → wait → screenshot**

```
# Kill running app
key("alt+f4")  # targeted at dotaclassic window

# Rebuild
dotnet build d2c-launcher.csproj 2>&1 | tail -20

# Relaunch (detached so it stays running)
start dotnet run --project d2c-launcher.csproj

# Wait for startup (~10s for Steam auth + content fetch)
sleep 10

# Verify
screenshot("dotaclassic")         # confirm app is up
click(settings gear coordinates)  # open settings modal
screenshot("dotaclassic")         # confirm new control appears
```

Repeat from "edit" if layout needs adjustments.

### 6. Functional check
- Interact with the new control via `click(x, y)`
- Screenshot after interaction to confirm state change
- Optionally verify `config.cfg` was written correctly
