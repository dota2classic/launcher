using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.ViewModels;

public partial class HeroOnMapViewModel : ObservableObject
{
    public string SteamId { get; }
    public int Team { get; }

    [ObservableProperty] private Thickness _heroMargin;
    [ObservableProperty] private bool _isDead;

    [ObservableProperty] private string _heroShortName = "";

    // Canonical canvas: both minimaps render at 320×320, scaled by a Viewbox to their display size.
    public const double CanvasSize = 320;

    // Hero icon size in the large (detail) minimap, in canvas pixels.
    public const double HeroIconSize = 28;

    // Small card displays the canvas at this size via Viewbox.
    // Small-card icon size in XAML = HeroIconSize * CanvasSize / SmallDisplaySize = 64.
    public const double SmallDisplaySize = 140;
    public const double SmallHeroIconSize = HeroIconSize * CanvasSize / SmallDisplaySize; // = 64

    private const double HalfSize = HeroIconSize / 2;

    public HeroOnMapViewModel(string steamId, string heroName, int team, double posX, double posY, bool isDead)
    {
        SteamId = steamId;
        Team = team;
        _heroShortName = ResolveShortName(heroName);
        _heroMargin = ComputeMargin(posX, posY);
        _isDead = isDead;
    }

    public void UpdatePosition(string heroName, double posX, double posY, bool isDead)
    {
        var url = ResolveShortName(heroName);
        if (url != HeroShortName) HeroShortName = url;
        HeroMargin = ComputeMargin(posX, posY);
        IsDead = isDead;
    }

    private static string ResolveShortName(string heroName) =>
        heroName.StartsWith("npc_dota_hero_", System.StringComparison.Ordinal)
            ? heroName["npc_dota_hero_".Length..]
            : heroName;

    private static double Remap(double v) => v * 0.90 + 0.06;
    private static Thickness ComputeMargin(double posX, double posY)
        => new Thickness(Remap(posX) * CanvasSize - HalfSize,
                         (1.0 - Remap(posY)) * CanvasSize - HalfSize, 0, 0);
}
