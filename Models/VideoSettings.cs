namespace d2c_launcher.Models;

/// <summary>
/// Display settings backed by dota/cfg/video.txt.
/// Separate from <see cref="GameLaunchSettings"/> which stores CLI flags only.
/// </summary>
public class VideoSettings
{
    public bool Fullscreen { get; set; }
    public bool NoWindowBorder { get; set; }
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
}
