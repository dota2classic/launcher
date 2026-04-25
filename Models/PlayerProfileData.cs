using System.Collections.Generic;

namespace d2c_launcher.Models;

public record PlayerProfileData(
    string Name,
    string? AvatarUrl,
    int Wins,
    int Losses,
    int Abandons,
    int TotalGames,
    int Mmr,
    int Rank,
    double AvgKills,
    double AvgDeaths,
    double AvgAssists,
    double SeasonAbandonRate,
    double SeasonPlaytimeSeconds,
    IReadOnlyList<AspectData> Aspects,
    string? RecalibrationStartedAt);
