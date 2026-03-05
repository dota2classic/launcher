namespace d2c_launcher.Models;

public class LauncherSettings
{
    public string? GameDirectory { get; set; }
    public string? BackendAccessToken { get; set; }

    /// <summary>
    /// The game directory for which a Windows Defender exclusion has already been
    /// requested. Null means no exclusion has been attempted yet.
    /// </summary>
    public string? DefenderExclusionPath { get; set; }

    /// <summary>Whether to automatically apply launcher updates on startup.</summary>
    public bool AutoUpdate { get; set; } = true;


}
