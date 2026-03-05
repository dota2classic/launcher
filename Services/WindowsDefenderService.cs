using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace d2c_launcher.Services;

/// <summary>
/// Attempts to add a Windows Defender exclusion path so that Defender does not scan
/// every file read/write during manifest generation and game downloads.
/// The operation is best-effort: failures (e.g. user cancels UAC) are silently ignored.
/// </summary>
public static class WindowsDefenderService
{
    /// <summary>
    /// Returns true if <paramref name="path"/> is already in the Windows Defender exclusion
    /// list. Runs without elevation; failures (Defender disabled, module absent) return false.
    /// </summary>
    public static async Task<bool> IsExcludedAsync(string path)
    {
        try
        {
            var escaped = path.Replace("'", "''");
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NonInteractive -Command \"if ((Get-MpPreference).ExclusionPath -contains '{escaped}') {{ exit 0 }} else {{ exit 1 }}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            using var process = Process.Start(psi);
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Spawns an elevated PowerShell process to add <paramref name="path"/> to the
    /// Windows Defender exclusion list. Triggers a UAC prompt; if the user cancels or
    /// the operation fails for any reason the method returns silently.
    /// </summary>
    public static async Task TryAddExclusionAsync(string path)
    {
        try
        {
            // Escape single quotes for PowerShell string literal
            var escaped = path.Replace("'", "''");
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NonInteractive -Command \"Add-MpPreference -ExclusionPath '{escaped}'\"",
                Verb = "RunAs",          // triggers UAC elevation prompt
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            using var process = Process.Start(psi);
            if (process != null)
                await process.WaitForExitAsync();
        }
        catch
        {
            // Silently ignore — exclusion is optional.
            // Common failure reasons: user cancelled UAC, Defender is disabled, no admin account.
        }
    }

    /// <summary>
    /// Spawns an elevated PowerShell process to remove <paramref name="path"/> from the
    /// Windows Defender exclusion list. Triggers a UAC prompt; failures are silently ignored.
    /// </summary>
    public static async Task TryRemoveExclusionAsync(string path)
    {
        try
        {
            var escaped = path.Replace("'", "''");
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NonInteractive -Command \"Remove-MpPreference -ExclusionPath '{escaped}'\"",
                Verb = "RunAs",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            using var process = Process.Start(psi);
            if (process != null)
                await process.WaitForExitAsync();
        }
        catch
        {
            // Silently ignore.
        }
    }
}
