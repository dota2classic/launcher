namespace d2c_launcher.Models;

public sealed class PartyMemberView
{
    public string SteamId { get; }
    public string Name { get; }
    public string? AvatarUrl { get; }
    public string Initials { get; }

    // Ban / access restrictions (from PlayerSummaryDto; defaults for leader who has no summary)
    public bool IsBanned { get; }
    public string? BannedUntil { get; }
    public bool CanPlayHumanGames { get; }
    public bool CanPlaySimpleModes { get; }
    public bool CanPlayEducation { get; }
    /// <summary>Season MMR. Null means unknown (e.g. party leader with no summary loaded).</summary>
    public int? Mmr { get; }

    public PartyMemberView(
        string steamId,
        string name,
        string? avatarUrl,
        bool isBanned = false,
        string? bannedUntil = null,
        bool canPlayHumanGames = true,
        bool canPlaySimpleModes = true,
        bool canPlayEducation = true,
        int? mmr = null)
    {
        SteamId = steamId;
        Name = name;
        AvatarUrl = avatarUrl;
        Initials = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[0].ToString().ToUpperInvariant();
        IsBanned = isBanned;
        BannedUntil = bannedUntil;
        CanPlayHumanGames = canPlayHumanGames;
        CanPlaySimpleModes = canPlaySimpleModes;
        CanPlayEducation = canPlayEducation;
        Mmr = mmr;
    }
}
