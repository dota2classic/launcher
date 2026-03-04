using System.Diagnostics;
using System.Threading.Tasks;

namespace d2c_launcher.Services;

public static class DefenderExclusionService
{
    /// <summary>
    /// Adds <paramref name="path"/> to Windows Defender exclusions via an elevated PowerShell process.
    /// Returns true if the process exited successfully (exit code 0).
    /// </summary>
    public static async Task<bool> AddExclusionAsync(string path)
    {
        // Escape single quotes in path (edge case)
        var escapedPath = path.Replace("'", "''");

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -Command \"Add-MpPreference -ExclusionPath '{escapedPath}'\"",
            Verb = "runas",           // triggers UAC elevation prompt
            UseShellExecute = true,   // required for Verb = runas
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        using var proc = Process.Start(psi);
        if (proc == null)
            return false;

        await proc.WaitForExitAsync();
        return proc.ExitCode == 0;
    }
}
