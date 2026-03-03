# Active Context

## Current Focus

**Adding `HardwareInfoService`** — WMI-based hardware enumeration for diagnostics and HWID generation.

### What It Does
- Queries WMI classes: `Win32_Processor`, `Win32_BaseBoard`, `Win32_DiskDrive`, `Win32_NetworkAdapterConfiguration`, `Win32_PhysicalMemory`, `Win32_VideoController`, `Win32_OperatingSystem`
- Computes a SHA-256 **HWID** from CPU ID + mobo serial + disk serials + MAC addresses
- Logs everything via `AppLog.Info()` tagged with `[HW]`
- Static class (`HardwareInfoService.LogAll()`) — no DI needed

### Files Changed (uncommitted)
| File | Change |
|------|--------|
| `Services/HardwareInfoService.cs` | New — WMI queries + HWID computation |
| `Program.cs` | Modified — likely calls `HardwareInfoService.LogAll()` at startup |
| `d2c-launcher.csproj` | Modified — adds `System.Management` NuGet reference |

### What's Still Needed
- Verify `Program.cs` wires up `HardwareInfoService.LogAll()` at startup
- Consider sending HWID to backend API (for analytics or ban enforcement)

---

## Recent Completed Work (from git log)

| Commit | Change |
|--------|--------|
| `dc61ce4` | Add test step to CI workflow |
| `212c9ca` | Remove .claude from git, add to .gitignore |
| `e1f4024` | Add xUnit test project (CfgGenerator, DotaCfgReader, CvarMappings) |
| `b26db9b` | Bi-directional config sync with `host_writeconfig` flush + colorblind setting |
| `48de235` | Language + NoVid launch settings in settings modal |

---

## Decisions Made Recently

- **Two-phase sync:** `host_writeconfig` → 1.5s delay → read config.cfg. Required because Source engine's `WM_COPYDATA` handler queues commands asynchronously.
- **Static HardwareInfoService:** No interface/DI needed — it's a diagnostic utility called once at startup.
- **`System.Management`:** Added as NuGet package for WMI access; Windows-only, which is fine (this app is Windows-only).
