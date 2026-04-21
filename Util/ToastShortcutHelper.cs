using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace d2c_launcher.Util;

/// <summary>
/// Ensures a Start Menu shortcut exists with the AppUserModelID property set.
/// Windows requires this for unpackaged Win32 apps to show toast notification popups
/// (without it, notifications are silently queued until the process gains focus).
/// </summary>
internal static class ToastShortcutHelper
{
    internal const string Aumid = "DotaClassic.Launcher";

    private static readonly string ProgramsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Windows\Start Menu\Programs");

    private static readonly string ShortcutPath = Path.Combine(ProgramsDir, "dotaclassic.lnk");

    // Legacy names created by older builds — cleaned up on next launch.
    private static readonly string[] LegacyShortcutNames = ["d2c-launcher.lnk", "Dotaclassic.lnk"];

    private static readonly Guid CLSID_ShellLink = new("00021401-0000-0000-C000-000000000046");

    // PKEY_AppUserModel_ID = {9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}, 5
    private static readonly PropertyKey PKEY_AppUserModel_ID =
        new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5u);

    public static void EnsureShortcut()
    {
        var exePath = Environment.ProcessPath!;

        // Always clean up legacy shortcuts regardless of whether the current one is valid —
        // a stale d2c-launcher.lnk with the same AUMID causes Windows to show "d2c-launcher"
        // as the toast app name instead of "dotaclassic".
        foreach (var name in LegacyShortcutNames)
        {
            var legacy = Path.Combine(ProgramsDir, name);
            if (File.Exists(legacy))
                try { File.Delete(legacy); } catch { /* best-effort */ }
        }

        if (File.Exists(ShortcutPath) && ShortcutTargetMatches(exePath))
            return;

        try
        {
            var link = (IShellLinkW)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_ShellLink)!)!;
            link.SetPath(exePath);
            link.SetArguments("");
            link.SetWorkingDirectory(Path.GetDirectoryName(exePath)!);
            link.SetIconLocation(exePath, 0);

            var store = (IPropertyStore)link;
            // VT_LPWSTR = 31; InitPropVariantFromString is an inline SDK helper not exported from propsys.dll
            var pv = new PropVariant { vt = 31, ptr = Marshal.StringToCoTaskMemUni(Aumid) };
            var key = PKEY_AppUserModel_ID;
            try
            {
                store.SetValue(ref key, ref pv);
                store.Commit();
            }
            finally
            {
                Marshal.FreeCoTaskMem(pv.ptr);
            }

            ((IPersistFile)link).Save(ShortcutPath, true);
            AppLog.Info($"Toast shortcut created: {ShortcutPath}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to create toast notification shortcut.", ex);
        }
    }

    private static bool ShortcutTargetMatches(string exePath)
    {
        try
        {
            var link = (IShellLinkW)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_ShellLink)!)!;
            ((IPersistFile)link).Load(ShortcutPath, 0);
            var sb = new StringBuilder(260);
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            return string.Equals(sb.ToString(), exePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([MarshalAs(UnmanagedType.LPWStr), Out] StringBuilder pszFile, int cch, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([MarshalAs(UnmanagedType.LPWStr), Out] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr), Out] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([MarshalAs(UnmanagedType.LPWStr), Out] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([MarshalAs(UnmanagedType.LPWStr), Out] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, Guid("0000010B-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        int GetCount(out uint cProps);
        int GetAt(uint iProp, out PropertyKey pkey);
        int GetValue(ref PropertyKey key, out PropVariant pv);
        int SetValue(ref PropertyKey key, ref PropVariant pv);
        int Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;
        public PropertyKey(Guid fmtid, uint pid) { this.fmtid = fmtid; this.pid = pid; }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr ptr;
    }
}
