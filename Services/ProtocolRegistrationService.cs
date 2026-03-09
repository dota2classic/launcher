using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

/// <summary>
/// Registers the <c>d2c://</c> custom URL protocol in the current user's registry
/// so Windows routes d2c:// links to this launcher.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ProtocolRegistrationService
{
    private const string ProtocolScheme = "d2c";

    public static void EnsureRegistered()
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
                AppLog.Info("[Protocol] could not determine exe path — skipping registration");
                return;
            }

            // HKCU\Software\Classes\d2c
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProtocolScheme}");
            key.SetValue("", $"URL:{ProtocolScheme} Protocol");
            key.SetValue("URL Protocol", "");

            // HKCU\Software\Classes\d2c\shell\open\command
            using var cmdKey = key.CreateSubKey(@"shell\open\command");
            cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");

            AppLog.Info($"[Protocol] registered d2c:// → {exePath}");
        }
        catch (Exception ex)
        {
            AppLog.Error("[Protocol] failed to register URL protocol", ex);
        }
    }
}
