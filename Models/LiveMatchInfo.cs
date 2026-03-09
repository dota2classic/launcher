namespace d2c_launcher.Models;

/// <summary>Info about a currently-running live match, used for spectating.</summary>
public record LiveMatchInfo(int MatchId, string Server);
