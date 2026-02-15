using Avalonia.Media.Imaging;

namespace d2c_launcher.Models;

public sealed class PartyMemberView
{
    public string SteamId { get; }
    public string Name { get; }
    public string? AvatarUrl { get; }
    public Bitmap? AvatarImage { get; }

    public PartyMemberView(string steamId, string name, string? avatarUrl, Bitmap? avatarImage)
    {
        SteamId = steamId;
        Name = name;
        AvatarUrl = avatarUrl;
        AvatarImage = avatarImage;
    }
}
