using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Api;

namespace d2c_launcher.ViewModels;

public partial class LiveMatchCardViewModel : ObservableObject
{
    public int MatchId { get; }

    [ObservableProperty] private string _duration = "0:00";
    [ObservableProperty] private int _playerCount;
    [ObservableProperty] private int _radiantScore;
    [ObservableProperty] private int _direScore;

    public ObservableCollection<HeroOnMapViewModel> Heroes { get; } = [];
    public ObservableCollection<LivePlayerRowViewModel> RadiantPlayers { get; } = [];
    public ObservableCollection<LivePlayerRowViewModel> DirePlayers { get; } = [];

    public LiveMatchCardViewModel(int matchId)
    {
        MatchId = matchId;
    }

    /// <summary>Full update: heroes on map + player rows + summary fields.</summary>
    public void UpdateFrom(LiveMatchDto dto)
    {
        UpdateSummaryFrom(dto);

        // Update hero map positions in place to keep Avalonia transitions firing
        var presentIds = new HashSet<string>();
        foreach (var slot in dto.Heroes)
        {
            if (slot.HeroData == null) continue;
            var id = slot.User.SteamId;
            presentIds.Add(id);
            var existing = Heroes.FirstOrDefault(h => h.SteamId == id);
            if (existing != null)
                existing.UpdatePosition(slot.HeroData.Pos_x, slot.HeroData.Pos_y, slot.HeroData.Respawn_time > 0);
            else
                Heroes.Add(new HeroOnMapViewModel(id, slot.HeroData.Hero, (int)slot.Team,
                    slot.HeroData.Pos_x, slot.HeroData.Pos_y, slot.HeroData.Respawn_time > 0));
        }
        for (int i = Heroes.Count - 1; i >= 0; i--)
            if (!presentIds.Contains(Heroes[i].SteamId))
                Heroes.RemoveAt(i);

        // Update player rows in place (keyed by SteamId) to avoid collection Reset flicker
        int radiantKills = 0, direKills = 0;
        foreach (var slot in dto.Heroes.OrderBy(s => s.User.Name))
        {
            var id = slot.User.SteamId;
            var team = (int)slot.Team;
            var collection = team == 2 ? RadiantPlayers : team == 3 ? DirePlayers : null;
            if (collection == null) continue;

            var existing = collection.FirstOrDefault(r => r.SteamId == id);
            if (existing != null)
                existing.UpdateFrom(slot);
            else
                collection.Add(new LivePlayerRowViewModel(slot));

            var kills = (int)(slot.HeroData?.Kills ?? 0);
            if (team == 2) radiantKills += kills;
            else direKills += kills;
        }
        // Remove rows for players no longer in the match
        var allIds = dto.Heroes.Select(s => s.User.SteamId).ToHashSet();
        for (int i = RadiantPlayers.Count - 1; i >= 0; i--)
            if (!allIds.Contains(RadiantPlayers[i].SteamId)) RadiantPlayers.RemoveAt(i);
        for (int i = DirePlayers.Count - 1; i >= 0; i--)
            if (!allIds.Contains(DirePlayers[i].SteamId)) DirePlayers.RemoveAt(i);

        RadiantScore = radiantKills;
        DireScore = direKills;
    }

    /// <summary>Updates only the sidebar summary fields (duration, score). Used by the list poll.</summary>
    public void UpdateSummaryFrom(LiveMatchDto dto)
    {
        Duration = FormatDuration((int)dto.Duration);
        PlayerCount = dto.Heroes.Count;
    }

    private static string FormatDuration(int seconds)
    {
        var m = seconds / 60;
        var s = seconds % 60;
        return $"{m}:{s:D2}";
    }
}
