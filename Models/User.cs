namespace d2c_launcher.Models;

public class User
{
    public ulong SteamId { get; }

    /// <summary>Steam32 account ID (SteamID64 − base offset). Used in API paths and URLs.</summary>
    public ulong SteamId32 => SteamId - 76561197960265728UL;
    public string PersonaName { get; }
    public byte[]? AvatarRgba { get; }
    public int AvatarWidth { get; }
    public int AvatarHeight { get; }

    public User(ulong steamId, string personaName, byte[]? avatarRgba = null, int avatarWidth = 0, int avatarHeight = 0)
    {
        SteamId = steamId;
        PersonaName = personaName;
        AvatarRgba = avatarRgba;
        AvatarWidth = avatarWidth;
        AvatarHeight = avatarHeight;
    }
}
