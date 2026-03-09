using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace d2c_launcher.Views.Components;

/// <summary>
/// Full-screen overlay that darkens everything except a rectangular spotlight cutout.
/// </summary>
public class SpotlightOverlay : Control
{
    public static readonly StyledProperty<Rect> SpotlightRectProperty =
        AvaloniaProperty.Register<SpotlightOverlay, Rect>(nameof(SpotlightRect));

    public Rect SpotlightRect
    {
        get => GetValue(SpotlightRectProperty);
        set => SetValue(SpotlightRectProperty, value);
    }

    static SpotlightOverlay()
    {
        AffectsRender<SpotlightOverlay>(SpotlightRectProperty);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var dimBrush = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0));

        var spot = SpotlightRect.Inflate(10);

        if (spot.Width <= 0 || spot.Height <= 0)
        {
            context.DrawRectangle(dimBrush, null, bounds);
            return;
        }

        // Dim overlay with a hole punched out
        var fullGeom   = new RectangleGeometry(bounds);
        var cutoutGeom = new RectangleGeometry(spot);
        var combined   = new CombinedGeometry(GeometryCombineMode.Exclude, fullGeom, cutoutGeom);
        context.DrawGeometry(dimBrush, null, combined);

        // Blue highlight border around the spotlight
        var highlightPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 77, 169, 243)), 2);
        context.DrawRectangle(null, highlightPen, spot);
    }
}
