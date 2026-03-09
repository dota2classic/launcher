"""
patch-client-dll.py — Patch ConVar flags in Dota 2 Classic client.dll

Usage:
    python patch-client-dll.py --find <cvar_name>
    python patch-client-dll.py --set-flags <cvar_name> <hex_flags>
    python patch-client-dll.py --add-flag <cvar_name> <hex_flag>
    python patch-client-dll.py --remove-flag <cvar_name> <hex_flag>

Examples:
    python patch-client-dll.py --find dota_camera_distance
    python patch-client-dll.py --remove-flag dota_camera_distance 0x4000
    python patch-client-dll.py --add-flag dota_use_particle_fow 0x4000
    python patch-client-dll.py --set-flags dota_camera_distance 0x0

Common flags:
    0x0008  FCVAR_CLIENTDLL
    0x0040  FCVAR_ARCHIVE
    0x4000  FCVAR_CHEAT

The script auto-detects the DLL path from common install locations, or you can
set the DLL_PATH variable below. A .bak backup is created before the first write.
"""

import struct
import sys
import os
import shutil

# --------------------------------------------------------------------------- #
# Config
# --------------------------------------------------------------------------- #

DLL_SEARCH_PATHS = [
    r"C:\Users\enchantinggg4\Games\Dota 6.84\dota\bin\client.dll",
]

IMAGE_BASE = 0x10000000
SECTIONS = [
    (".text",  0x1000,    0xcd6a00, 0x400),
    (".rdata", 0xcd8000,  0x50e600, 0xcd6e00),
    (".data",  0x11e7000, 0x107e00, 0x11e5400),
]

# --------------------------------------------------------------------------- #
# Helpers
# --------------------------------------------------------------------------- #

def find_dll():
    for p in DLL_SEARCH_PATHS:
        if os.path.exists(p):
            return p
    raise FileNotFoundError(f"client.dll not found in search paths: {DLL_SEARCH_PATHS}")


def file_offset_to_va(data, offset):
    for _, va, size, raw in SECTIONS:
        if raw <= offset < raw + size:
            return IMAGE_BASE + va + (offset - raw)
    return None


def find_flags_offset(data, cvar_name: str):
    """
    Returns the file offset of the 32-bit flags immediate for the given cvar,
    or raises if not found.
    """
    name_bytes = cvar_name.encode() + b"\x00"
    str_offset = data.find(name_bytes)
    if str_offset == -1:
        raise ValueError(f"Cvar string '{cvar_name}' not found in binary")

    str_va = file_offset_to_va(data, str_offset)
    if str_va is None:
        raise ValueError(f"String offset {hex(str_offset)} not in any known section")

    ptr_bytes = struct.pack("<I", str_va)

    # Search for pointer in .text section (constructor code)
    text_raw_start = 0x400
    text_raw_end   = 0x400 + 0xcd6a00

    candidates = []
    idx = text_raw_start
    while True:
        idx = data.find(ptr_bytes, idx, text_raw_end)
        if idx == -1:
            break
        candidates.append(idx)
        idx += 1

    if not candidates:
        raise ValueError(f"No pointer to '{cvar_name}' string found in .text")

    # For each candidate, scan nearby bytes for a `mov [addr], imm32` (c7 05 ...)
    # that looks like a flags write (value < 0x100000, not code-like)
    for ptr_offset in candidates:
        window_start = max(text_raw_start, ptr_offset - 64)
        window_end   = min(text_raw_end, ptr_offset + 64)
        window = data[window_start:window_end]

        for i in range(len(window) - 9):
            if window[i] == 0xc7 and window[i+1] == 0x05:
                imm_offset = window_start + i + 6
                imm_val = struct.unpack_from("<I", data, imm_offset)[0]
                # Plausible flags: small value, not ASCII text
                if imm_val < 0x100000 and not (0x20202020 <= imm_val <= 0x7f7f7f7f):
                    return imm_offset, imm_val

    raise ValueError(f"Could not locate flags instruction for '{cvar_name}'")


def backup(dll_path):
    bak = dll_path + ".bak"
    if not os.path.exists(bak):
        shutil.copy2(dll_path, bak)
        print(f"Backed up to {bak}")
    else:
        print(f"Backup already exists: {bak}")


def read_dll(dll_path):
    with open(dll_path, "rb") as f:
        return bytearray(f.read())


def write_dll(dll_path, data):
    with open(dll_path, "wb") as f:
        f.write(data)


# --------------------------------------------------------------------------- #
# Commands
# --------------------------------------------------------------------------- #

def cmd_find(dll_path, cvar_name):
    data = read_dll(dll_path)
    offset, flags = find_flags_offset(data, cvar_name)
    print(f"  cvar:         {cvar_name}")
    print(f"  flags offset: {hex(offset)}")
    print(f"  flags value:  {hex(flags)}")
    known = {0x0008: "FCVAR_CLIENTDLL", 0x0040: "FCVAR_ARCHIVE", 0x4000: "FCVAR_CHEAT"}
    active = [name for val, name in known.items() if flags & val]
    print(f"  active flags: {', '.join(active) if active else '(none)'}")


def cmd_set_flags(dll_path, cvar_name, new_flags):
    data = read_dll(dll_path)
    offset, current = find_flags_offset(data, cvar_name)
    print(f"  {cvar_name}: {hex(current)} -> {hex(new_flags)}  (offset {hex(offset)})")
    backup(dll_path)
    struct.pack_into("<I", data, offset, new_flags)
    write_dll(dll_path, data)
    print("  Done.")


def cmd_add_flag(dll_path, cvar_name, flag):
    data = read_dll(dll_path)
    offset, current = find_flags_offset(data, cvar_name)
    new_flags = current | flag
    print(f"  {cvar_name}: {hex(current)} -> {hex(new_flags)}  (offset {hex(offset)})")
    backup(dll_path)
    struct.pack_into("<I", data, offset, new_flags)
    write_dll(dll_path, data)
    print("  Done.")


def cmd_remove_flag(dll_path, cvar_name, flag):
    data = read_dll(dll_path)
    offset, current = find_flags_offset(data, cvar_name)
    new_flags = current & ~flag
    print(f"  {cvar_name}: {hex(current)} -> {hex(new_flags)}  (offset {hex(offset)})")
    backup(dll_path)
    struct.pack_into("<I", data, offset, new_flags)
    write_dll(dll_path, data)
    print("  Done.")


# --------------------------------------------------------------------------- #
# Entry point
# --------------------------------------------------------------------------- #

def usage():
    print(__doc__)
    sys.exit(1)


if __name__ == "__main__":
    args = sys.argv[1:]
    if len(args) < 2:
        usage()

    dll_path = find_dll()
    print(f"DLL: {dll_path}\n")

    cmd = args[0]
    cvar = args[1]

    if cmd == "--find":
        cmd_find(dll_path, cvar)
    elif cmd == "--set-flags" and len(args) == 3:
        cmd_set_flags(dll_path, cvar, int(args[2], 16))
    elif cmd == "--add-flag" and len(args) == 3:
        cmd_add_flag(dll_path, cvar, int(args[2], 16))
    elif cmd == "--remove-flag" and len(args) == 3:
        cmd_remove_flag(dll_path, cvar, int(args[2], 16))
    else:
        usage()
