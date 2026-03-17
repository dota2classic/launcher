using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.ViewModels;

public partial class HeroOnMapViewModel : ObservableObject
{
    public string SteamId { get; }
    public int Team { get; }

    [ObservableProperty] private Thickness _heroMargin;
    [ObservableProperty] private bool _isDead;

    private readonly string _shortHeroName;
    public string HeroImageUrl => $"https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/icons/{_shortHeroName}.png";

    private const double CanvasSize = 380;
    private const double HalfSize = 16;

    public HeroOnMapViewModel(string steamId, string heroName, int team, double posX, double posY, bool isDead)
    {
        SteamId = steamId;
        Team = team;
        _shortHeroName = heroName.StartsWith("npc_dota_hero_", System.StringComparison.Ordinal)
            ? heroName["npc_dota_hero_".Length..]
            : heroName;
        _heroMargin = ComputeMargin(posX, posY);
        _isDead = isDead;
    }

    public void UpdatePosition(double posX, double posY, bool isDead)
    {
        HeroMargin = ComputeMargin(posX, posY);
        IsDead = isDead;
    }

    private static double Remap(double v) => v * 0.94 + 0.02;
    private static double ComputeLeft(double posX) => Remap(posX) * CanvasSize - HalfSize;
    private static double ComputeTop(double posY) => (1.0 - Remap(posY)) * CanvasSize - HalfSize;
    private static Thickness ComputeMargin(double posX, double posY)
        => new Thickness(ComputeLeft(posX), ComputeTop(posY), 0, 0);
}
