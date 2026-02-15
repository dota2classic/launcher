namespace d2c_launcher.Models;

public class User
{
    public ulong SteamId { get; }
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
