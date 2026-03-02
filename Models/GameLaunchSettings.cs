namespace d2c_launcher.Models;

public class GameLaunchSettings
{
    // CLI flags (-flag)
    public bool NoVid { get; set; } = true;
    public bool Console { get; set; } = true;

    // Cfg cvars (+exec d2c_launch.cfg)
    public int? FpsMax { get; set; }

    // Escape hatches for power users
    public string? ExtraArgs { get; set; }
    public string? CustomCfgLines { get; set; }
}
