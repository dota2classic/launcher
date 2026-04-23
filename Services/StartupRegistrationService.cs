using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using d2c_launcher.Util;
using Microsoft.Win32;

namespace d2c_launcher.Services;

[SupportedOSPlatform("windows")]
public sealed class StartupRegistrationService : IStartupRegistrationService
{
    internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ValueName = "D2C Launcher";
    internal const string BackgroundStartArg = "--background-start";

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (key == null)
            {
                AppLog.Error("[Startup] Could not open or create HKCU Run key");
                return;
            }

            if (enabled)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    AppLog.Error("[Startup] Could not determine launcher exe path");
                    return;
                }

                key.SetValue(ValueName, BuildRunCommand(exePath), RegistryValueKind.String);
                AppLog.Info($"[Startup] Enabled auto-launch: {exePath}");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                AppLog.Info("[Startup] Disabled auto-launch");
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("[Startup] Failed to update auto-launch registration", ex);
        }
    }

    internal static string BuildRunCommand(string exePath) =>
        $"\"{exePath}\" {BackgroundStartArg}";
}
