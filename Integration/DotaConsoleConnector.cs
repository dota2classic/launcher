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
                AppLog.Info($"DotaConsoleConnector: sent '{command}'");
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
