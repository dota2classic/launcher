namespace d2c_launcher.Models;

public sealed class MatchmakingModeInfo
{
    public int ModeId { get; }
    public string Name { get; }

    public MatchmakingModeInfo(int modeId, string name)
    {
        ModeId = modeId;
        Name = name;
    }
}
