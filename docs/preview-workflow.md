# Preview Workflow

How to visually verify UI changes without running the full launcher (no Steam required).

## Two tools available

| Tool | When to use |
|------|-------------|
| **Component Preview** (`tools/preview.ps1`) | Iterating on an Avalonia component — fast rebuild, shows component in isolation |
| **HTML Screenshot** (`tools/screenshot-html.ps1`) | Rendering mockups or design references from `tools/mockups/` |

---

## Component Preview

Launches a minimal Avalonia shell that renders a single registered component.

```powershell
# From repo root — always use powershell, not pwsh
powershell -ExecutionPolicy Bypass -File tools/preview.ps1 <ComponentName>
```

### Available components

| Name | What it shows |
|------|---------------|
| `PartyPanel` | Party member list with invite button |
| `QueueButton` | Matchmaking search/cancel button |
| `GameSearchPanel` | Mode checkboxes (3 mock modes) |
| `AcceptGameModal` | Ready-check accept/decline dialog |
| `NotificationArea` | Floating invite notifications |
| `LaunchSteamFirst` | "Launch Steam first" screen |
| `SelectGame` | Game directory picker screen |

### Workflow

1. Make your XAML / ViewModel changes
2. Run the preview script — it builds incrementally, then opens the window
3. Use `Read` tool on the screenshot path printed by the script to view the result
4. Repeat from step 1 until it looks right

### Adding a new component

- Register it in `Preview/PreviewRegistry.cs`
- Add any stub services it needs in `Preview/PreviewStubs.cs`

---

## HTML Screenshot

Renders an HTML file via headless Chrome and saves a PNG.

```powershell
# Basic usage (defaults: 1000×800)
powershell -ExecutionPolicy Bypass -File tools/screenshot-html.ps1 tools/mockups/some-mockup.html

# Custom size
powershell -ExecutionPolicy Bypass -File tools/screenshot-html.ps1 tools/mockups/some-mockup.html -Width 1200 -Height 900
```

- Output goes to `tools/screenshots/<timestamp>.png`
- Use `Read` tool on the returned path to view the image
- Chrome binary: `C:\Program Files\Google\Chrome\Application\chrome.exe`

HTML mockups live in `tools/mockups/` — use them as design references when building Avalonia UI.

---

## Tips

- Always call `Read` on the screenshot path — do not assume the result looks correct without viewing it
- The preview tool does not require Steam to be running
- Build errors surface in the terminal output from the script — fix them before inspecting the screenshot
