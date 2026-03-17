using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Api;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class LivePlayerRowViewModel : ObservableObject
{
    public string SteamId { get; }
    public string Name { get; }
    public string HeroImageUrl { get; }

    [ObservableProperty] private int _kills;
    [ObservableProperty] private int _deaths;
    [ObservableProperty] private int _assists;
    [ObservableProperty] private int _level;
    [ObservableProperty] private bool _isDead;
    [ObservableProperty] private string _kdaText = "";
    [ObservableProperty] private double _healthPercent;
    [ObservableProperty] private IReadOnlyList<string?> _itemUrls = [null, null, null, null, null, null];

    // doubles for KDA bar proportions
    public double KillsD   => Kills;
    public double DeathsD  => Deaths;
    public double AssistsD => Assists;

    public LivePlayerRowViewModel(MatchSlotInfo slot)
    {
        SteamId = slot.User.SteamId;
        var isBot = long.TryParse(slot.User.SteamId, out var sid) && sid <= 10;
        Name = isBot ? $"Бот #{sid + 1}" : slot.User.Name;
        var heroName = slot.HeroData?.Hero ?? "";
        var shortName = heroName.StartsWith("npc_dota_hero_", System.StringComparison.Ordinal)
            ? heroName["npc_dota_hero_".Length..]
            : heroName;
        HeroImageUrl = string.IsNullOrEmpty(shortName)
            ? ""
            : $"https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/icons/{shortName}.png";
        UpdateFrom(slot);
    }

    public void UpdateFrom(MatchSlotInfo slot)
    {
        Kills = (int)(slot.HeroData?.Kills ?? 0);
        Deaths = (int)(slot.HeroData?.Deaths ?? 0);
        Assists = (int)(slot.HeroData?.Assists ?? 0);
        Level = (int)(slot.HeroData?.Level ?? 0);
        IsDead = (slot.HeroData?.Respawn_time ?? 0) > 0;
        KdaText = $"{Kills} / {Deaths} / {Assists}";
        var maxHp = slot.HeroData?.Max_health ?? 0;
        HealthPercent = maxHp > 0 ? (slot.HeroData!.Health / maxHp) * 100.0 : 0;

        var h = slot.HeroData;
        ItemUrls = h == null ? [null, null, null, null, null, null] :
        [
            DotaItemData.GetItemImageUrl((int)h.Item0),
            DotaItemData.GetItemImageUrl((int)h.Item1),
            DotaItemData.GetItemImageUrl((int)h.Item2),
            DotaItemData.GetItemImageUrl((int)h.Item3),
            DotaItemData.GetItemImageUrl((int)h.Item4),
            DotaItemData.GetItemImageUrl((int)h.Item5),
        ];
    }
}
