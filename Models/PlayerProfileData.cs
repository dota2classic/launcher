namespace d2c_launcher.Models;

public record PlayerProfileData(
    string Name,
    string? AvatarUrl,
    int Wins,
    int Losses,
    int Abandons,
    int Mmr,
    int Rank,
    double AvgKills,
    double AvgDeaths,
    double AvgAssists);
