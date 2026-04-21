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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);
    public const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // --- IPropertyStore COM interop for taskbar icon control ---

    [DllImport("shell32.dll", SetLastError = true)]
    public static extern int SHGetPropertyStoreForWindow(
        IntPtr hwnd, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore propertyStore);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    // System.AppUserModel.ID — {9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}, 5
    public static readonly PROPERTYKEY PKEY_AppUserModel_ID = new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 5
    };

    // System.AppUserModel.RelaunchIconResource — {9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}, 3
    public static readonly PROPERTYKEY PKEY_AppUserModel_RelaunchIconResource = new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 3
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct PROPVARIANT : IDisposable
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr pwszVal;

        public static PROPVARIANT FromString(string value)
        {
            return new PROPVARIANT
            {
                vt = 31, // VT_LPWSTR
                pwszVal = Marshal.StringToCoTaskMemUni(value)
            };
        }

        public void Dispose()
        {
            if (vt == 31 /* VT_LPWSTR */ && pwszVal != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pwszVal);
                pwszVal = IntPtr.Zero;
                vt = 0;
            }
        }
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        int GetCount(out uint cProps);
        int GetAt(uint iProp, out PROPERTYKEY pkey);
        int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        int SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);
        int Commit();
    }
}
