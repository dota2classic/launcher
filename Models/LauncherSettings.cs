using System.Collections.Generic;

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

    /// <summary>
    /// True once the user has responded to the Windows Defender exclusion prompt
    /// (either accepted or skipped). When true the prompt is never shown again,
    /// regardless of whether an exclusion is actually present.
    /// </summary>
    public bool DefenderPromptAnswered { get; set; }

    /// <summary>Whether to automatically apply launcher updates on startup.</summary>
    public bool AutoUpdate { get; set; } = true;

    /// <summary>
    /// IDs of optional DLC packages the user has chosen to install.
    /// Null means the user has never been shown the DLC selector (show it on next download).
    /// </summary>
    public List<string>? SelectedDlcIds { get; set; }

    /// <summary>
    /// IDs of ALL packages (required + optional) that were actually downloaded and installed.
    /// Null means the game has never been downloaded via the package system.
    /// </summary>
    public List<string>? InstalledPackageIds { get; set; }
}
