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

    /// <summary>
    /// True when the Windows Defender exclusion prompt should be shown.
    /// Prompt is skipped if the user already responded, or if a legacy exclusion path is set
    /// (backwards compat: users who accepted before <see cref="DefenderPromptAnswered"/> existed).
    /// </summary>
    public bool ShouldShowDefenderPrompt => !DefenderPromptAnswered && DefenderExclusionPath == null;

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

    /// <summary>
    /// True once the user has seen the first-run introduction overlay.
    /// </summary>
    public bool IntroShown { get; set; }

    /// <summary>
    /// When true, pressing the window close button hides the app to the system tray
    /// instead of shutting it down.
    /// </summary>
    public bool CloseToTray { get; set; } = true;
}
