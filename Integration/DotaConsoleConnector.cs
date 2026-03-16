using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using d2c_launcher.Util;

namespace d2c_launcher.Integration;

/// <summary>
/// Sends console commands to a running Dota 2 instance via WM_COPYDATA.
/// </summary>
[SupportedOSPlatform("windows")]
public static class DotaConsoleConnector
{
    private const string DotaWindowTitle = "DOTA 2";

    public static bool IsWindowOpen()
        => WinApi.FindWindowA(null, DotaWindowTitle) != IntPtr.Zero;

    /// <summary>
    /// Sets the taskbar icon for the DOTA 2 window by writing AppUserModel
    /// properties via <c>IPropertyStore</c>. This is the modern Windows API for
    /// controlling per-window taskbar appearance — it works cross-process and is
    /// not overwritten by the game's own WNDCLASS registration.
    /// </summary>
    public static void SetWindowIcon(string exePath)
    {
        var hwnd = WinApi.FindWindowA(null, DotaWindowTitle);
        if (hwnd == IntPtr.Zero)
            return;

        var iid = typeof(WinApi.IPropertyStore).GUID;
        var hr = WinApi.SHGetPropertyStoreForWindow(hwnd, ref iid, out var store);
        if (hr != 0 || store == null)
        {
            AppLog.Info($"DotaConsoleConnector.SetWindowIcon: SHGetPropertyStoreForWindow failed hr=0x{hr:X8}");
            return;
        }

        try
        {
            // Give the window its own taskbar identity so it doesn't merge with the launcher
            var aumidKey = WinApi.PKEY_AppUserModel_ID;
            var aumidVal = WinApi.PROPVARIANT.FromString("DotaClassic.Dota2");
            try
            {
                var hrSet = store.SetValue(ref aumidKey, ref aumidVal);
                if (hrSet != 0)
                    AppLog.Info($"DotaConsoleConnector.SetWindowIcon: SetValue(AUMID) failed hr=0x{hrSet:X8}");
            }
            finally { aumidVal.Dispose(); }

            // Tell the taskbar to use dota.exe's embedded icon
            var iconKey = WinApi.PKEY_AppUserModel_RelaunchIconResource;
            var iconVal = WinApi.PROPVARIANT.FromString($"{exePath},0");
            try
            {
                var hrSet = store.SetValue(ref iconKey, ref iconVal);
                if (hrSet != 0)
                    AppLog.Info($"DotaConsoleConnector.SetWindowIcon: SetValue(RelaunchIconResource) failed hr=0x{hrSet:X8}");
            }
            finally { iconVal.Dispose(); }

            var commitHr = store.Commit();
            if (commitHr != 0)
                AppLog.Info($"DotaConsoleConnector.SetWindowIcon: Commit failed hr=0x{commitHr:X8}");
            else
                AppLog.Info($"DotaConsoleConnector.SetWindowIcon: set AppUserModel properties on hwnd={hwnd}");
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    /// <summary>Sends a console command string to the Dota 2 window. Returns false if the window was not found.</summary>
    public static bool SendCommand(string command)
    {
        var windowPtr = WinApi.FindWindowA(null, DotaWindowTitle);
        if (windowPtr == IntPtr.Zero)
        {
            AppLog.Info($"DotaConsoleConnector: window '{DotaWindowTitle}' not found");
            return false;
        }

        var bytes = Encoding.ASCII.GetBytes(command + "\0");
        var ptrData = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, ptrData, bytes.Length);

            var cds = new WinApi.COPYDATASTRUCT
            {
                dwData = IntPtr.Zero,
                cbData = bytes.Length,
                lpData = ptrData
            };

            var pCds = Marshal.AllocHGlobal(Marshal.SizeOf(cds));
            try
            {
                Marshal.StructureToPtr(cds, pCds, false);
                WinApi.SendMessageA(windowPtr, WinApi.WM_COPYDATA, IntPtr.Zero, pCds);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(pCds);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(ptrData);
        }
    }
}
