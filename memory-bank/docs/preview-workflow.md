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

Run the tool with an unknown name (or no name) to print the full list. Key components:

| Name | What it shows |
|------|---------------|
| `LauncherHeader` | Top header bar (avatar + name + buttons) |
| `LauncherHeaderPlay` | Header — idle state (ИГРАТЬ) |
| `LauncherHeaderStop` | Header — game running state (СТОП) |
| `PartyPanel` | Party member list with invite button |
| `QueueButton` | Matchmaking search/cancel button |
| `QueueButtonSingle` | Queue button isolated in a fixed container |
| `GameSearchPanel` | Mode checkboxes (3 mock modes) |
| `AcceptGameModal` / `AcceptGameModal1/2/5/10` | Ready-check dialog (various player counts) |
| `NotificationArea` | Floating notification stack |
| `PleaseGoQueue` | "Go queue" toast in notification area |
| `AchievementToast` | Achievement unlock toast |
| `AbandonButtonConnect` / `AbandonButtonSearching` | Queue + abandon button row |
| `InviteModal` | Invite player modal |
| `ProfilePanel` | Player profile (stats + hero list) |
| `ChatPanel` | Chat panel |
| `RichMessage` | Rich text segments (URLs, rarity tags) |
| `LivePanel` | Live matches minimap + player lists |
| `LivePlayerRowDead` | Live player row (alive vs dead state) |
| `Minimap` | Minimap canvas only |
| `SettingsPanel` | Settings modal |
| `Loading` | Loading/connecting screen |
| `LaunchSteamFirst` | "Launch Steam first" screen |
| `SelectGame` | Game directory picker screen |
| `GameDownload` / `GameDownloadError` | Download/verify progress screen |

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
