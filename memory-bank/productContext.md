# Product Context

## Why This Exists

Dotaclassic is a community effort to preserve and play the old Dota 2 (Source 1 era). Valve deprecated this version and provides no official launcher or matchmaking. The community maintains custom servers at `api.dotaclassic.ru` and needs a purpose-built launcher to:

1. Generate a valid Steam auth ticket (requires live Steam session)
2. Connect players to community matchmaking
3. Launch the correct game binary with proper arguments
4. Keep the launcher itself up to date automatically

## User Journey

```
App starts
    │
    ▼
Is Steam running?  ──No──▶  LaunchSteamFirstView  (prompt user to open Steam)
    │Yes
    ▼
Is game directory set?  ──No──▶  SelectGameView  (folder picker)
    │Yes
    ▼
MainLauncherView
    ├── Select game mode(s)
    ├── Enter matchmaking queue
    ├── Accept ready check
    ├── Game launches automatically
    └── Settings panel (game cvars + launch flags)
```

## UX Principles

- **Minimal friction:** If Steam is running and the game is installed, the user should reach the main screen in under 2 seconds
- **Real-time feedback:** Queue position, party status, and game search updates arrive via Socket.IO without user action
- **Russian language:** All UI text is in Cyrillic. Uses `I18n` system (`Resources/Locales/ru.json`, `I18n.T()`, `{l:T}` XAML extension)
- **Reliable auth:** SteamBridge subprocess approach is used specifically because trimmed .NET publish breaks Steamworks.NET when loaded in-process. Reliability over simplicity
- **Settings parity:** Users expect the launcher to reflect and respect their in-game settings. The bi-directional config sync ensures the launcher stays in sync even when the user changes settings inside the game

## What Users Care About

1. Getting into a game quickly and reliably
2. Their settings (sensitivity, fps cap, keybinds) being preserved
3. The launcher updating itself — no manual downloads
4. Party play with friends
