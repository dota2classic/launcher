using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Api;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class LivePlayerRowViewModel : ObservableObject
{
    public string SteamId { get; }
    public string Name { get; }
    public bool IsBot { get; }

    /// <summary>Called when user clicks the player name. Receives steam32 ID. Null for bots.</summary>
    public Action<string>? OpenProfileAction { get; set; }

    [ObservableProperty] private string _heroImageUrl = "";

    [ObservableProperty] private int _kills;
    [ObservableProperty] private int _deaths;
    [ObservableProperty] private int _assists;
    [ObservableProperty] private int _level;
    [ObservableProperty] private bool _isDead;
    [ObservableProperty] private string _kdaText = "";
    [ObservableProperty] private double _healthPercent;
    [ObservableProperty] private IReadOnlyList<string?> _itemUrls = [null, null, null, null, null, null];

    // doubles for KDA bar proportions — must be notified when the int properties change
    [ObservableProperty] private double _killsD;
    [ObservableProperty] private double _deathsD;
    [ObservableProperty] private double _assistsD;

    public LivePlayerRowViewModel(MatchSlotInfo slot)
    {
        SteamId = slot.User.SteamId;
        long.TryParse(slot.User.SteamId, out var sid);
        IsBot = slot.HeroData?.Bot == true || sid <= 10;
        Name = IsBot ? "Бот" : slot.User.Name;
        UpdateFrom(slot);
    }

    [RelayCommand]
    private void OpenProfile()
    {
        if (!IsBot) OpenProfileAction?.Invoke(SteamId);
    }

    private static string ResolveHeroUrl(string? heroName)
    {
        if (string.IsNullOrEmpty(heroName)) return "avares://d2c-launcher/Assets/Images/Heroes/default.webp";
        var shortName = heroName.StartsWith("npc_dota_hero_", System.StringComparison.Ordinal)
            ? heroName["npc_dota_hero_".Length..]
            : heroName;
        return string.IsNullOrEmpty(shortName)
            ? "avares://d2c-launcher/Assets/Images/Heroes/default.webp"
            : $"avares://d2c-launcher/Assets/Images/Heroes/{shortName}.webp";
    }

    public void UpdateFrom(MatchSlotInfo slot)
    {
        var url = ResolveHeroUrl(slot.HeroData?.Hero);
        if (url != HeroImageUrl) HeroImageUrl = url;
        Kills = (int)(slot.HeroData?.Kills ?? 0);
        Deaths = (int)(slot.HeroData?.Deaths ?? 0);
        Assists = (int)(slot.HeroData?.Assists ?? 0);
        KillsD = Kills;
        DeathsD = Deaths;
        AssistsD = Assists;
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
