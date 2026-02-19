using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace d2c_launcher.Util;

[SupportedOSPlatform("windows")]
internal static class WinApi
{
    public const int WM_COPYDATA = 0x004A;

    [StructLayout(LayoutKind.Sequential)]
    public struct COPYDATASTRUCT
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern IntPtr FindWindowA(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    public static extern IntPtr SendMessageA(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
}
