# Project Brief: D2C Launcher

## What It Is

**D2C Launcher** is a Windows desktop application for **Dota 2 Classic** — a community-maintained server running the original Dota 2 on the **Source 1 engine**. It provides the full lifecycle needed to play on the community server: authentication, matchmaking, and game launch.

## Problem It Solves

Dota 2 Classic is a community project with no official support from Valve. Players need:
- A way to authenticate with Steam without official game integration
- Matchmaking against other community players
- Validation that the game is correctly installed
- A way to launch the game with the correct settings

## Core Features

| Feature | Status |
|---------|--------|
| Steam authentication (via SteamBridge subprocess) | Done |
| Matchmaking queue with real-time updates | Done |
| Party management (invite, accept, leave) | Done |
| Game install validation | Done |
| Game launching with Source 1 arguments | Done |
| In-launcher game settings (cvars) | Done |
| Bi-directional config.cfg sync | Done |
| Application auto-updates | Done |
| Hardware info logging / HWID | In progress |

## Platform & Scope

- **Platform:** Windows only (x64), no other targets
- **Runtime:** .NET 10.0 self-contained
- **User base:** Russian-speaking Dota 2 Classic community
- **Backend API:** `https://api.dotaclassic.ru`
- **Steam App ID:** 480 (Spacewar/demo) — used for auth ticket generation

## Non-Goals

- Cross-platform support
- Official Dota 2 / Valve integration
- General-purpose game launcher
