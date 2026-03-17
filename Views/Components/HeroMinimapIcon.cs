using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Views.Components;

/// <summary>
/// Renders a single hero icon from the minimap spritesheet.
/// Bind HeroName to a short hero name ("axe") or full internal name ("npc_dota_hero_axe").
/// </summary>
public class HeroMinimapIcon : Control
{
    private static readonly Bitmap? Sheet;

    static HeroMinimapIcon()
    {
        AffectsRender<HeroMinimapIcon>(HeroNameProperty);
        try
        {
            var uri = new System.Uri("avares://d2c-launcher/Assets/Images/minimap_hero_sheet.png");
            using var stream = AssetLoader.Open(uri);
            Sheet = new Bitmap(stream);
        }
        catch (Exception ex) { AppLog.Error("HeroMinimapIcon: failed to load minimap spritesheet", ex); }
    }

    public static readonly StyledProperty<string?> HeroNameProperty =
        AvaloniaProperty.Register<HeroMinimapIcon, string?>(nameof(HeroName));

    public string? HeroName
    {
        get => GetValue(HeroNameProperty);
        set => SetValue(HeroNameProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Sheet is null || string.IsNullOrEmpty(HeroName)) return;
        if (!HeroSpriteMap.TryGetOffset(HeroName, out var off)) return;

        var src = new Rect(off.X, off.Y, 32, 32);
        var dst = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.DrawImage(Sheet, src, dst);
    }

    protected override Size MeasureOverride(Size availableSize) => new(32, 32);
}
