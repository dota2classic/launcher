using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Api;

namespace d2c_launcher.ViewModels;

public partial class LiveMatchCardViewModel : ObservableObject
{
    private static readonly Dictionary<int, string> GameModeNames = new()
    {
        { 0, "Нет" },
        { 1, "All Pick" },
        { 2, "Captains Mode" },
        { 3, "Random Draft" },
        { 4, "Single Draft" },
        { 5, "All Random" },
        { 8, "Reverse CM" },
        { 9, "Greeviling" },
        { 11, "All Draft" },
        { 12, "Least Played" },
        { 13, "New Player Pool" },
        { 16, "Captains Draft" },
        { 18, "Ability Draft" },
        { 20, "All Random Deathmatch" },
        { 21, "1v1 Mid" },
        { 22, "All Pick Ranked" },
        { 23, "Turbo" },
    };

    private static readonly Dictionary<int, string> GameStateLabels = new()
    {
        { 0, "Инициализация" },
        { 1, "Загрузка игроков" },
        { 2, "Выбор героев" },
        { 3, "Выбор героев" },
        { 4, "Начало игры" },
        { 5, "Игра идет" },
        { 6, "Игра завершена" },
        { 7, "Ошибка загрузки" },
    };

    public long MatchId { get; }

    [ObservableProperty] private string _duration = "0:00";
    [ObservableProperty] private int _playerCount;
    [ObservableProperty] private int _radiantScore;
    [ObservableProperty] private int _direScore;
    [ObservableProperty] private string _modeLabel = "";
    [ObservableProperty] private string _gameStateLabel = "Игра идет";
    [ObservableProperty] private string _server = "";
    [ObservableProperty] private string _title = "";

    public ObservableCollection<HeroOnMapViewModel> Heroes { get; } = [];
    public ObservableCollection<LivePlayerRowViewModel> RadiantPlayers { get; } = [];
    public ObservableCollection<LivePlayerRowViewModel> DirePlayers { get; } = [];

    public LiveMatchCardViewModel(long matchId)
    {
        MatchId = matchId;
        Title = $"Матч {matchId}";
    }

    /// <summary>Full update: heroes on map + player rows + summary fields.</summary>
    public void UpdateFrom(LiveMatchDto dto, string matchmakingModeName)
    {
        UpdateSummaryFrom(dto, matchmakingModeName);

        // Update hero map positions in place
        var presentIds = new HashSet<string>();
        foreach (var slot in dto.Heroes)
        {
            if (slot.HeroData == null) continue;
            var id = slot.User?.SteamId;
            if (string.IsNullOrEmpty(id)) continue;
            presentIds.Add(id);
            var existing = Heroes.FirstOrDefault(h => h.SteamId == id);
            if (existing != null)
                existing.UpdatePosition(slot.HeroData.Hero, slot.HeroData.Pos_x, slot.HeroData.Pos_y, slot.HeroData.Respawn_time > 0);
            else
                Heroes.Add(new HeroOnMapViewModel(id, slot.HeroData.Hero, (int)slot.Team,
                    slot.HeroData.Pos_x, slot.HeroData.Pos_y, slot.HeroData.Respawn_time > 0));
        }
        for (int i = Heroes.Count - 1; i >= 0; i--)
            if (!presentIds.Contains(Heroes[i].SteamId))
                Heroes.RemoveAt(i);

        // Update player rows in place
        int radiantKills = 0, direKills = 0;
        foreach (var slot in dto.Heroes.OrderBy(s => s.User?.Name))
        {
            var id = slot.User?.SteamId;
            if (string.IsNullOrEmpty(id)) continue;
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
        var allIds = dto.Heroes.Select(s => s.User.SteamId).ToHashSet();
        for (int i = RadiantPlayers.Count - 1; i >= 0; i--)
            if (!allIds.Contains(RadiantPlayers[i].SteamId)) RadiantPlayers.RemoveAt(i);
        for (int i = DirePlayers.Count - 1; i >= 0; i--)
            if (!allIds.Contains(DirePlayers[i].SteamId)) DirePlayers.RemoveAt(i);

        RadiantScore = radiantKills;
        DireScore = direKills;
    }

    /// <summary>Updates summary fields only (used by list poll).</summary>
    public void UpdateSummaryFrom(LiveMatchDto dto, string matchmakingModeName)
    {
        Duration = FormatDuration((int)dto.Duration);
        PlayerCount = dto.Heroes.Count;
        Server = dto.Server;

        var gameModeName = GameModeNames.TryGetValue((int)dto.GameMode, out var gmn) ? gmn : $"Режим {(int)dto.GameMode}";
        ModeLabel = $"{matchmakingModeName}, {gameModeName}";

        var stateLabel = GameStateLabels.TryGetValue((int)dto.GameState, out var sl) ? sl : "Игра идет";
        GameStateLabel = stateLabel;
        Title = $"Матч {MatchId} - {stateLabel}";
    }

    private static string FormatDuration(int seconds)
    {
        var m = seconds / 60;
        var s = seconds % 60;
        return $"{m}:{s:D2}";
    }
}
