# client.dll Binary Patching

Tooling and process for patching `client.dll` in Dota 2 Classic (Source 1 engine, Windows x86 32-bit).

See `tools/patch-client-dll.py` for the ready-to-use script.

---

## Background

### ConVar flags

Each ConVar has a 32-bit flags field. `FCVAR_CHEAT = 0x4000` means the engine resets the cvar to its default whenever `sv_cheats` changes from 1 to 0 (e.g. on connect to any non-cheat server). Removing this flag from `client.dll` makes the cvar behave like a normal cvar — persists across connects, settable via launch args or console freely.

The reverse is also possible: adding `FCVAR_CHEAT` to a cvar that doesn't have it forces it to reset on every server connect.

### How flags are stored in this build

ConVar objects are **not** static data. They are initialized at runtime by `mov [addr], imm32` sequences in the `.text` section. The flags value is a plain 32-bit immediate in one of these instructions. To find and patch it you need to:

1. Locate the cvar name string in `.rdata`
2. Calculate its virtual address (VA)
3. Find the pointer to that VA in `.text` (the constructor code)
4. Read the surrounding instructions to find the flags `mov`
5. Patch the immediate value

The script `tools/patch-client-dll.py` automates all of this.

---

## PE layout (Dota 6.84 client.dll)

| Section  | VA          | Raw offset   | Size        |
|----------|-------------|--------------|-------------|
| `.text`  | `0x1000`    | `0x400`      | `0xcd6a00`  |
| `.rdata` | `0xcd8000`  | `0xcd6e00`   | `0x50e600`  |
| `.data`  | `0x11e7000` | `0x11e5400`  | `0x107e00`  |

Image base: `0x10000000`

---

## FCVAR flags reference

| Flag              | Value    | Meaning                              |
|-------------------|----------|--------------------------------------|
| `FCVAR_CLIENTDLL` | `0x0008` | Defined in client.dll                |
| `FCVAR_ARCHIVE`   | `0x0040` | Saved to config.cfg                  |
| `FCVAR_CHEAT`     | `0x4000` | Requires sv_cheats 1                 |

---

## How to find a cvar's flags offset

Run `tools/patch-client-dll.py --find <cvar_name>` (see script). Manually:

```python
import struct

dll_path = r'C:\...\client.dll'
with open(dll_path, 'rb') as f:
    data = f.read()

image_base = 0x10000000
sections = [
    ('.text',  0x1000,    0xcd6a00, 0x400),
    ('.rdata', 0xcd8000,  0x50e600, 0xcd6e00),
]

# 1. Find string and calculate VA
name = b'your_cvar_name\x00'
idx = data.find(name)
for sname, va, size, raw in sections:
    if raw <= idx < raw + size:
        str_va = image_base + va + (idx - raw)

# 2. Search for pointer to string in .text
ptr_bytes = struct.pack('<I', str_va)
pidx = data.find(ptr_bytes)  # find in .text range

# 3. Dump surrounding bytes to locate the flags mov instruction
# Look for pattern: c7 05 [4 bytes addr] [4 bytes flags]
# The flags immediate is 6 bytes after the c7 05 opcode
start = max(0, pidx - 32)
chunk = data[start : pidx + 64]
for i in range(0, len(chunk), 16):
    row = chunk[i:i+16]
    print(f'{hex(start+i)}: {" ".join(f"{b:02x}" for b in row)}')
```

Once you identify the `c7 05 xx xx xx xx YY YY YY YY` instruction where `YY YY YY YY` is the flags value, the flags offset is `instruction_offset + 6`.

---

## Workflow: patching and shipping

Patches are applied **once by a developer** and the patched `client.dll` is uploaded to the CDN/launcher backend. All users receive the pre-patched file through the normal sync — no per-user patching, no launcher code changes needed.

1. Obtain a fresh `client.dll` from the game build
2. Run `tools/patch-client-dll.py` to apply the desired flag changes
3. Upload the patched `client.dll` to the backend (update the manifest hash to match)
4. Users sync normally and get the patched file

This means:
- **No hardcoded camera distance** in the binary — users set `dota_camera_distance` freely via launch args, console, or config, and it persists across server connects because `FCVAR_CHEAT` is stripped
- **No launcher-side patching logic** — the launcher treats `client.dll` like any other synced file
- When the game is updated and a new `client.dll` is shipped, re-run the script against the new binary before uploading
