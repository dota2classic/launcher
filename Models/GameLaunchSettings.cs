namespace d2c_launcher.Models;

/// <summary>
/// Launcher-specific settings persisted as JSON.
/// Game cvars live in <see cref="CvarSettings"/> backed by config.cfg.
/// </summary>
public class GameLaunchSettings
{
    // CLI flags (-flag)
    public bool NoVid { get; set; } = false;
    public string Language { get; set; } = "russian";

    // Escape hatches for power users
    public string? ExtraArgs { get; set; }
    public string? CustomCfgLines { get; set; }
}
