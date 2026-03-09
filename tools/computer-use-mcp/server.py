"""
Computer-use MCP server for d2c-launcher debugging.
Exposes screenshot, click, type, key, scroll, and window-management tools.
"""

import io
import os
import tempfile
import ctypes
import ctypes.wintypes
import pyautogui
import mss
import mss.tools
from PIL import Image
from mcp.server.fastmcp import FastMCP

pyautogui.FAILSAFE = False  # don't abort when mouse hits corner
pyautogui.PAUSE = 0.05

mcp = FastMCP("computer-use")

# ---------------------------------------------------------------------------
# Win32 helpers
# ---------------------------------------------------------------------------

user32 = ctypes.windll.user32

class RECT(ctypes.Structure):
    _fields_ = [("left", ctypes.c_long), ("top", ctypes.c_long),
                ("right", ctypes.c_long), ("bottom", ctypes.c_long)]


def _find_window(title_fragment: str = "d2c-launcher") -> int | None:
    """Return the HWND of the first window whose title contains title_fragment."""
    found = []

    @ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.wintypes.HWND, ctypes.wintypes.LPARAM)
    def cb(hwnd, _):
        length = user32.GetWindowTextLengthW(hwnd)
        if length:
            buf = ctypes.create_unicode_buffer(length + 1)
            user32.GetWindowTextW(hwnd, buf, length + 1)
            if title_fragment.lower() in buf.value.lower():
                found.append(hwnd)
        return True

    user32.EnumWindows(cb, 0)
    return found[0] if found else None


def _get_window_rect(hwnd: int) -> tuple[int, int, int, int]:
    rect = RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(rect))
    return rect.left, rect.top, rect.right, rect.bottom


def _screenshot_window(hwnd: int) -> bytes:
    """Capture just the window area, return PNG bytes."""
    user32.SetForegroundWindow(hwnd)
    left, top, right, bottom = _get_window_rect(hwnd)
    w, h = right - left, bottom - top
    with mss.mss() as sct:
        region = {"left": left, "top": top, "width": w, "height": h}
        raw = sct.grab(region)
        img = Image.frombytes("RGB", raw.size, raw.bgra, "raw", "BGRX")
        buf = io.BytesIO()
        img.save(buf, format="PNG")
        return buf.getvalue()


def _screenshot_full() -> bytes:
    with mss.mss() as sct:
        raw = sct.grab(sct.monitors[0])
        img = Image.frombytes("RGB", raw.size, raw.bgra, "raw", "BGRX")
        buf = io.BytesIO()
        img.save(buf, format="PNG")
        return buf.getvalue()



_SCREENSHOT_DIR = os.path.join(tempfile.gettempdir(), "computer-use-mcp-screenshots")


def _save_png(png_bytes: bytes) -> str:
    """Save PNG bytes to a temp file and return the path."""
    os.makedirs(_SCREENSHOT_DIR, exist_ok=True)
    # Clean up old screenshots
    for f in os.listdir(_SCREENSHOT_DIR):
        try:
            os.remove(os.path.join(_SCREENSHOT_DIR, f))
        except OSError:
            pass
    path = os.path.join(_SCREENSHOT_DIR, "screenshot.png")
    with open(path, "wb") as fh:
        fh.write(png_bytes)
    return path


# ---------------------------------------------------------------------------
# Tools
# ---------------------------------------------------------------------------

@mcp.tool()
def screenshot(window_title: str = "d2c-launcher") -> dict:
    """
    Capture the application window and return a base64-encoded PNG.
    If the window is not found, captures the full screen instead.
    Pass window_title="" to always capture the full screen.
    """
    if window_title:
        hwnd = _find_window(window_title)
        if hwnd:
            png = _screenshot_window(hwnd)
            left, top, right, bottom = _get_window_rect(hwnd)
            path = _save_png(png)
            return {
                "screenshot_path": path,
                "window_found": True,
                "window_rect": {"left": left, "top": top, "right": right, "bottom": bottom},
                "size": {"width": right - left, "height": bottom - top},
            }
    png = _screenshot_full()
    path = _save_png(png)
    return {"screenshot_path": path, "window_found": False}


@mcp.tool()
def get_window_rect(window_title: str = "d2c-launcher") -> dict:
    """
    Return the bounding box (left, top, right, bottom) of the application window.
    Useful for computing click coordinates relative to the window.
    """
    hwnd = _find_window(window_title)
    if not hwnd:
        return {"error": f"Window '{window_title}' not found"}
    left, top, right, bottom = _get_window_rect(hwnd)
    return {"left": left, "top": top, "right": right, "bottom": bottom,
            "width": right - left, "height": bottom - top}


@mcp.tool()
def focus_window(window_title: str = "d2c-launcher") -> dict:
    """Bring the application window to the foreground."""
    hwnd = _find_window(window_title)
    if not hwnd:
        return {"error": f"Window '{window_title}' not found"}
    user32.SetForegroundWindow(hwnd)
    return {"ok": True}


@mcp.tool()
def click(x: int, y: int, button: str = "left") -> dict:
    """
    Click at absolute screen coordinates (x, y).
    button: "left" | "right" | "middle"
    Use get_window_rect to find the window position, then add offsets.
    """
    pyautogui.click(x, y, button=button)
    return {"ok": True, "x": x, "y": y, "button": button}


@mcp.tool()
def double_click(x: int, y: int) -> dict:
    """Double-click at absolute screen coordinates (x, y)."""
    pyautogui.doubleClick(x, y)
    return {"ok": True, "x": x, "y": y}


@mcp.tool()
def right_click(x: int, y: int) -> dict:
    """Right-click at absolute screen coordinates (x, y)."""
    pyautogui.rightClick(x, y)
    return {"ok": True, "x": x, "y": y}


@mcp.tool()
def type_text(text: str) -> dict:
    """
    Type text into the currently focused element.
    Tip: click the target element first.
    """
    pyautogui.typewrite(text, interval=0.03)
    return {"ok": True, "text": text}


@mcp.tool()
def key(keys: str) -> dict:
    """
    Press a key or key combination.
    Examples: "enter", "escape", "tab", "ctrl+a", "ctrl+c", "alt+f4"
    Uses pyautogui.hotkey() for combinations, pyautogui.press() for single keys.
    """
    if "+" in keys:
        parts = keys.split("+")
        pyautogui.hotkey(*parts)
    else:
        pyautogui.press(keys)
    return {"ok": True, "keys": keys}


@mcp.tool()
def scroll(x: int, y: int, clicks: int = 3) -> dict:
    """
    Scroll at absolute screen coordinates (x, y).
    clicks > 0 = scroll up, clicks < 0 = scroll down.
    """
    pyautogui.scroll(clicks, x=x, y=y)
    return {"ok": True, "x": x, "y": y, "clicks": clicks}


@mcp.tool()
def move_mouse(x: int, y: int) -> dict:
    """Move the mouse to absolute screen coordinates without clicking."""
    pyautogui.moveTo(x, y)
    return {"ok": True, "x": x, "y": y}


if __name__ == "__main__":
    mcp.run()
