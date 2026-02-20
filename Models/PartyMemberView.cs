using Avalonia.Media.Imaging;

namespace d2c_launcher.Models;

public sealed class PartyMemberView
{
    public string SteamId { get; }
    public string Name { get; }
    public string? AvatarUrl { get; }
    public Bitmap? AvatarImage { get; }

    // Ban / access restrictions (from PlayerSummaryDto; defaults for leader who has no summary)
    public bool IsBanned { get; }
    public string? BannedUntil { get; }
    public bool CanPlayHumanGames { get; }
    public bool CanPlaySimpleModes { get; }
    public bool CanPlayEducation { get; }

    public PartyMemberView(
        string steamId,
        string name,
        string? avatarUrl,
        Bitmap? avatarImage,
        bool isBanned = false,
        string? bannedUntil = null,
        bool canPlayHumanGames = true,
        bool canPlaySimpleModes = true,
        bool canPlayEducation = true)
    {
        SteamId = steamId;
        Name = name;
        AvatarUrl = avatarUrl;
        AvatarImage = avatarImage;
        IsBanned = isBanned;
        BannedUntil = bannedUntil;
        CanPlayHumanGames = canPlayHumanGames;
        CanPlaySimpleModes = canPlaySimpleModes;
        CanPlayEducation = canPlayEducation;
    }
}
