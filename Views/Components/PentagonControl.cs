using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using d2c_launcher.Models;

namespace d2c_launcher.Views.Components;

/// <summary>
/// Renders a pentagonal feedback (radar) chart from 5 aspect ratings — v2 design.
/// Grid rings at 25/50/75/100%, gold data polygon, colored dots.
/// </summary>
public sealed class PentagonControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<AspectData>?> AspectsProperty =
        AvaloniaProperty.Register<PentagonControl, IReadOnlyList<AspectData>?>(nameof(Aspects));

    public IReadOnlyList<AspectData>? Aspects
    {
        get => GetValue(AspectsProperty);
        set => SetValue(AspectsProperty, value);
    }

    static PentagonControl()
    {
        AffectsRender<PentagonControl>(AspectsProperty);
    }

    private static readonly IReadOnlyDictionary<string, (string Label, bool IsNegative)> AspectMeta =
        new Dictionary<string, (string, bool)>(StringComparer.OrdinalIgnoreCase)
        {
            ["FRIENDLY"]  = ("Добряк",   false),
            ["TALKATIVE"] = ("Болтун",   false),
            ["OPTIMIST"]  = ("Оптимист", false),
            ["TOXIC"]     = ("Токсик",   true),
            ["CLOWN"]     = ("Клоун",    false),
        };

    public override void Render(DrawingContext ctx)
    {
        var aspects = Aspects;
        if (aspects == null || aspects.Count == 0)
            return;

        double w = Bounds.Width;
        double h = Bounds.Height;
        double cx = w / 2;
        double cy = h / 2;
        double radius = Math.Min(w, h) * 0.36;
        double labelRadius = radius * 1.32;

        // sort descending by count
        var sorted = new List<AspectData>(aspects);
        sorted.Sort((a, b) => b.Count.CompareTo(a.Count));
        while (sorted.Count < 5)
            sorted.Add(new AspectData("", 0));

        int n = 5;
        double maxCount = Math.Max(sorted[0].Count, 1);

        var center = new Point(cx, cy);

        // precompute outer ring points
        var outerPts = new Point[n];
        for (int i = 0; i < n; i++)
        {
            double angle = 2 * Math.PI / n * i - Math.PI / 2;
            outerPts[i] = new Point(cx + Math.Cos(angle) * radius, cy + Math.Sin(angle) * radius);
        }

        var gridPen  = new Pen(new SolidColorBrush(Color.Parse("#1E2530")), 1);
        var spokePen = new Pen(new SolidColorBrush(Color.Parse("#1E2530")), 1);

        // draw grid rings at 25%, 50%, 75%, 100%
        foreach (var pct in new[] { 0.25, 0.5, 0.75, 1.0 })
        {
            var ringPts = new Point[n];
            for (int i = 0; i < n; i++)
            {
                double angle = 2 * Math.PI / n * i - Math.PI / 2;
                ringPts[i] = new Point(cx + Math.Cos(angle) * radius * pct, cy + Math.Sin(angle) * radius * pct);
            }
            ctx.DrawGeometry(null, gridPen, BuildPolygon(ringPts));
        }

        // draw spokes
        for (int i = 0; i < n; i++)
            ctx.DrawLine(spokePen, center, outerPts[i]);

        // data polygon
        var dataPts = new Point[n];
        for (int i = 0; i < n; i++)
        {
            double mag = Math.Max(0.12, sorted[i].Count / maxCount);
            double angle = 2 * Math.PI / n * i - Math.PI / 2;
            dataPts[i] = new Point(cx + Math.Cos(angle) * radius * mag, cy + Math.Sin(angle) * radius * mag);
        }

        var dataFill   = new SolidColorBrush(Color.FromArgb(0x1A, 0xC8, 0xA8, 0x4B)); // gold 10%
        var dataStroke = new Pen(new SolidColorBrush(Color.FromArgb(0xB2, 0xC8, 0xA8, 0x4B)), 1.5); // gold 70%
        ctx.DrawGeometry(dataFill, dataStroke, BuildPolygon(dataPts));

        // dots + labels
        var typeface    = new Typeface(new FontFamily("avares://d2c-launcher/Assets/Fonts#Noto Sans"));
        var labelBrush  = new SolidColorBrush(Color.Parse("#48525A"));
        var dotGold     = new SolidColorBrush(Color.Parse("#C8A84B"));

        for (int i = 0; i < n; i++)
        {
            // dot
            var rawName = sorted[i].Name;
            ctx.DrawEllipse(dotGold, null, dataPts[i], 3.5, 3.5);

            // label
            if (string.IsNullOrEmpty(rawName)) continue;
            string displayName = AspectMeta.TryGetValue(rawName, out var m) ? m.Label : rawName;
            double angle = 2 * Math.PI / n * i - Math.PI / 2;
            double lx = cx + Math.Cos(angle) * labelRadius;
            double ly = cy + Math.Sin(angle) * labelRadius;

            var ft = new FormattedText(
                displayName,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                10,
                labelBrush);

            ctx.DrawText(ft, new Point(lx - ft.Width / 2, ly - ft.Height / 2));
        }

        // center dot
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#2D3842")), null, center, 2, 2);
    }

    private static Geometry BuildPolygon(Point[] pts)
    {
        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = pts[0], IsClosed = true, IsFilled = true };
        for (int i = 1; i < pts.Length; i++)
            fig.Segments!.Add(new LineSegment { Point = pts[i] });
        geo.Figures!.Add(fig);
        return geo;
    }
}
