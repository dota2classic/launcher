using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.ViewModels;

public partial class HeroOnMapViewModel : ObservableObject
{
    public string SteamId { get; }
    public int Team { get; }

    [ObservableProperty] private Thickness _heroMargin;
    [ObservableProperty] private Thickness _heroMarginSmall;
    [ObservableProperty] private Thickness _heroMarginMedium;
    [ObservableProperty] private bool _isDead;

    [ObservableProperty] private string _heroImageUrl = "";

    private const double CanvasSize = 380;
    private const double HalfSize = 16;

    public HeroOnMapViewModel(string steamId, string heroName, int team, double posX, double posY, bool isDead)
    {
        SteamId = steamId;
        Team = team;
        _heroImageUrl = ResolveHeroUrl(heroName);
        _heroMargin = ComputeMargin(posX, posY);
        _heroMarginSmall = ComputeMarginSmall(posX, posY);
        _heroMarginMedium = ComputeMarginMedium(posX, posY);
        _isDead = isDead;
    }

    public void UpdatePosition(string heroName, double posX, double posY, bool isDead)
    {
        var url = ResolveHeroUrl(heroName);
        if (url != HeroImageUrl) HeroImageUrl = url;
        HeroMargin = ComputeMargin(posX, posY);
        HeroMarginSmall = ComputeMarginSmall(posX, posY);
        HeroMarginMedium = ComputeMarginMedium(posX, posY);
        IsDead = isDead;
    }

    private static string ResolveHeroUrl(string heroName)
    {
        var shortName = heroName.StartsWith("npc_dota_hero_", System.StringComparison.Ordinal)
            ? heroName["npc_dota_hero_".Length..]
            : heroName;
        return string.IsNullOrEmpty(shortName)
            ? ""
            : $"https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/icons/{shortName}.png";
    }

    private const double SmallCanvasSize = 160;
    private const double SmallHalfSize = 5;

    private const double MediumCanvasSize = 320;
    private const double MediumHalfSize = 14;

    private static double Remap(double v) => v * 0.94 + 0.02;
    private static double ComputeLeft(double posX) => Remap(posX) * CanvasSize - HalfSize;
    private static double ComputeTop(double posY) => (1.0 - Remap(posY)) * CanvasSize - HalfSize;
    private static Thickness ComputeMargin(double posX, double posY)
        => new Thickness(ComputeLeft(posX), ComputeTop(posY), 0, 0);
    private static Thickness ComputeMarginSmall(double posX, double posY)
        => new Thickness(Remap(posX) * SmallCanvasSize - SmallHalfSize,
                         (1.0 - Remap(posY)) * SmallCanvasSize - SmallHalfSize, 0, 0);
    private static Thickness ComputeMarginMedium(double posX, double posY)
        => new Thickness(Remap(posX) * MediumCanvasSize - MediumHalfSize,
                         (1.0 - Remap(posY)) * MediumCanvasSize - MediumHalfSize, 0, 0);
}
