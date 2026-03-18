"""
patch-urls.py — Find and patch URL strings in Dota 2 Classic client.dll

Usage:
    python patch-urls.py <dll_path> --list
    python patch-urls.py <dll_path> --null <url>
    python patch-urls.py <dll_path> --replace <old_url> <new_url>

Examples:
    python patch-urls.py client.dll --list
    python patch-urls.py client.dll --null http://www.dota2.com
    python patch-urls.py client.dll --replace http://www.dota2.com/store http://dotaclassic.ru/store

Notes:
    - --null replaces the URL string with null bytes (disables the URL)
    - --replace requires the new URL to be shorter than or equal to the original;
      it is padded with null bytes to fill the original space
    - A .bak backup is created before the first write (same as patch-client-dll.py)
    - Run --list first to see what URLs are present and their exact byte strings
"""

import struct
import sys
import os
import shutil

# --------------------------------------------------------------------------- #
# Config — edit DLL_SEARCH_PATHS to match your install location
# --------------------------------------------------------------------------- #

DLL_SEARCH_PATHS = [
    r"C:\Users\enchantinggg4\Games\Dota 6.84\dota\bin\client.dll",
]

SEARCH_TERM = b"dota2.com"

# --------------------------------------------------------------------------- #
# Helpers
# --------------------------------------------------------------------------- #

def find_dll():
    for p in DLL_SEARCH_PATHS:
        if os.path.exists(p):
            return p
    raise FileNotFoundError(f"client.dll not found in search paths: {DLL_SEARCH_PATHS}")


def read_dll(dll_path):
    with open(dll_path, "rb") as f:
        return bytearray(f.read())


def write_dll(dll_path, data):
    with open(dll_path, "wb") as f:
        f.write(data)


def backup(dll_path):
    bak = dll_path + ".bak"
    if not os.path.exists(bak):
        shutil.copy2(dll_path, bak)
        print(f"Backed up to {bak}")
    else:
        print(f"Backup already exists: {bak}")


def extract_null_terminated(data, offset):
    """Read a null-terminated ASCII string starting at offset."""
    end = data.index(b"\x00", offset)
    return data[offset:end].decode("ascii", errors="replace")


def find_string_start(data, match_offset):
    """
    Walk backwards from match_offset to find the start of the null-terminated
    string that contains the match (i.e., find the preceding null byte or BOF).
    """
    i = match_offset
    while i > 0 and data[i - 1] != 0:
        i -= 1
    return i


def find_all_urls(data):
    """
    Return a list of (file_offset, full_string) for every null-terminated string
    in the binary that contains SEARCH_TERM.
    """
    results = []
    seen_offsets = set()
    idx = 0
    while True:
        idx = data.find(SEARCH_TERM, idx)
        if idx == -1:
            break
        start = find_string_start(data, idx)
        if start not in seen_offsets:
            try:
                s = extract_null_terminated(data, start)
                results.append((start, s))
                seen_offsets.add(start)
            except ValueError:
                pass  # no null terminator found — skip
        idx += len(SEARCH_TERM)
    return results


# --------------------------------------------------------------------------- #
# Commands
# --------------------------------------------------------------------------- #

def cmd_list(dll_path):
    data = read_dll(dll_path)
    urls = find_all_urls(data)
    if not urls:
        print(f"No strings containing '{SEARCH_TERM.decode()}' found.")
        return
    print(f"Found {len(urls)} string(s) containing '{SEARCH_TERM.decode()}':\n")
    for offset, s in urls:
        print(f"  offset {hex(offset):>12}  len={len(s)+1:4d}  {s!r}")


def cmd_null(dll_path, target_url):
    data = read_dll(dll_path)
    target_bytes = target_url.encode("ascii") + b"\x00"
    idx = data.find(target_bytes)
    if idx == -1:
        # Try without the trailing null (user may have omitted it)
        idx = data.find(target_url.encode("ascii"))
        if idx == -1:
            print(f"ERROR: URL not found in binary: {target_url!r}")
            sys.exit(1)

    original_len = len(target_url)
    print(f"  Nulling {original_len+1} bytes at offset {hex(idx)}: {target_url!r}")
    backup(dll_path)
    # Overwrite with null bytes (keep the length so surrounding code still works)
    data[idx:idx + original_len + 1] = b"\x00" * (original_len + 1)
    write_dll(dll_path, data)
    print("  Done.")


def cmd_replace(dll_path, old_url, new_url):
    if len(new_url) > len(old_url):
        print(f"ERROR: new URL ({len(new_url)} bytes) is longer than old URL ({len(old_url)} bytes).")
        print("       In-place replacement requires the new string to be <= original length.")
        sys.exit(1)

    data = read_dll(dll_path)
    target_bytes = old_url.encode("ascii") + b"\x00"
    idx = data.find(target_bytes)
    if idx == -1:
        idx = data.find(old_url.encode("ascii"))
        if idx == -1:
            print(f"ERROR: URL not found in binary: {old_url!r}")
            sys.exit(1)

    new_bytes = new_url.encode("ascii") + b"\x00"
    # Pad to original length so we don't shift any bytes
    padded = new_bytes + b"\x00" * (len(old_url) + 1 - len(new_bytes))

    print(f"  Replacing at offset {hex(idx)}:")
    print(f"    old ({len(old_url)+1} bytes): {old_url!r}")
    print(f"    new ({len(new_url)+1} bytes): {new_url!r}")
    backup(dll_path)
    data[idx:idx + len(padded)] = padded
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

    if not args or args[0] in ("-h", "--help"):
        usage()

    dll_path = find_dll()
    print(f"DLL: {dll_path}\n")

    cmd = args[0]

    if cmd == "--list" and len(args) == 1:
        cmd_list(dll_path)
    elif cmd == "--null" and len(args) == 2:
        cmd_null(dll_path, args[1])
    elif cmd == "--replace" and len(args) == 3:
        cmd_replace(dll_path, args[1], args[2])
    else:
        usage()
