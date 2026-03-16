# Taskbar Icon Investigation (Issue #79)

## Problem

When dota.exe is launched via the launcher (or any bat file / parent process), its taskbar button shows an empty/rectangle icon. When run directly from Explorer, the icon is correct.

## What We Tried

### 1. `UseShellExecute = true` (GameLaunchViewModel.cs)
**Status:** Kept (still correct to have), but **did not fix the icon**.
Routes launch through ShellExecuteEx — avoids job object inheritance, but does not affect AUMID or window icon.

### 2. `SetCurrentProcessExplicitAppUserModelID("DotaClassic.Launcher")` (Program.cs)
**Status:** Kept — **partially worked**.
Before this fix, dota.exe was merged into the launcher's taskbar group. After this fix, dota.exe gets its own separate taskbar button. But the button still shows an empty icon.

### 3. `WM_SETICON` via `SendMessageA` + `SetClassLongPtr`
**Status:** Does not work.
After the game window ("DOTA 2") appears, we:
- Call `ExtractIconEx(dota.exe, ...)` → returns `extracted=2`, valid `hLarge` and `hSmall` handles
- Send `WM_SETICON` (ICON_BIG + ICON_SMALL) via `SendMessageA`
- Call `SetClassLongPtr` with `GCL_HICON` + `GCL_HICONSM`

Icon remains blank. The calls succeed (no errors), but the taskbar does not update.

## Current State of the Code

Changes that remain in the codebase (all in `master`):

| File | Change |
|------|--------|
| `Program.cs` | `SetCurrentProcessExplicitAppUserModelID("DotaClassic.Launcher")` called at startup |
| `ViewModels/GameLaunchViewModel.cs` | `UseShellExecute = true`; fires `ApplyWindowIconAsync` after launch |
| `Integration/DotaConsoleConnector.cs` | `SetWindowIcon(exePath)` — ExtractIconEx + WM_SETICON + SetClassLongPtr |
| `Util/WinApi.cs` | Added `WM_SETICON`, `GCL_HICON`, `GCL_HICONSM`, `ExtractIconEx`, `DestroyIcon`, `SetClassLongPtr` |

The `SetWindowIcon` code path runs and logs correctly, but has no visible effect.

## Hypotheses Not Yet Tested

1. **Wrong window handle** — `FindWindowA(null, "DOTA 2")` may be finding an intermediate or non-primary window. The Source engine may create multiple top-level windows and the taskbar button belongs to a different one.

2. **Game overwrites icon after us** — The Source engine may reset window icons during its own initialization after our code runs. We run at `IsWindowOpen()` → first ping, but the engine may not be fully initialized at that point.

3. **`dota.exe` has no icon resource** — `ExtractIconEx` returns `extracted=2` which means it found icons in the file. But "correct icon when run from Explorer" may actually be Explorer showing a cached/stale icon rather than the live exe icon.

4. **DWM thumbnail vs window icon** — The taskbar may use the DWM thumbnail/preview system rather than `GCL_HICON` for the small button icon. Relevant API: `DwmSetIconicThumbnail`, `DwmSetIconicLivePreviewBitmap`.

5. **`ITaskbarList3` approach** — Setting the overlay icon via `ITaskbarList3::SetOverlayIcon` might be more reliable than `WM_SETICON` for the taskbar button.

## Root Cause (Best Guess)

The community dota.exe was likely compiled without an icon resource in its window class (`WNDCLASSEX.hIcon = NULL`). When Windows can't find a class or window icon, it falls back to... nothing (blank rectangle). Our `WM_SETICON` + `SetClassLongPtr` calls may be ignored or overwritten by the engine's own `RegisterClassEx` which runs in the game's thread after our cross-process calls.

## Verdict

Not fixed. The AUMID improvement (separate taskbar button) is a net win and should stay. The icon itself may require either:
- Patching dota.exe's PE resources to embed an icon (out of scope)
- A different approach involving `ITaskbarList3` COM interface
- Accepting the blank icon as a known limitation
