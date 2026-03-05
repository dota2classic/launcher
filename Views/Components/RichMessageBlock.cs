using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using d2c_launcher.Models;

namespace d2c_launcher.Views.Components;

/// <summary>
/// Renders a list of <see cref="RichSegment"/> items as mixed inline content
/// (plain text, colored rarity text, emoticon images, clickable URLs) inside a
/// <see cref="TextBlock"/>, preserving proper word-wrap behavior.
/// </summary>
public class RichMessageBlock : UserControl
{
    private readonly TextBlock _textBlock;
    // Maps character ranges to URLs for click detection.
    private readonly List<(int Start, int End, string Url)> _urlRanges = new();

    public static readonly StyledProperty<IReadOnlyList<RichSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<RichMessageBlock, IReadOnlyList<RichSegment>?>(nameof(Segments));

    public IReadOnlyList<RichSegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public RichMessageBlock()
    {
        _textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#b8bcc0")),
            Cursor = new Cursor(StandardCursorType.Arrow),
        };
        _textBlock.PointerReleased += OnTextBlockPointerReleased;
        _textBlock.PointerMoved += OnTextBlockPointerMoved;
        Content = _textBlock;
    }

    private void OnTextBlockPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_urlRanges.Count == 0) return;
        var pos = e.GetPosition(_textBlock);
        var hit = _textBlock.TextLayout?.HitTestPoint(pos);
        if (hit == null) return;
        var charPos = hit.Value.TextPosition;
        foreach (var (start, end, url) in _urlRanges)
        {
            if (charPos >= start && charPos < end)
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch { /* ignore */ }
                break;
            }
        }
    }

    private void OnTextBlockPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_urlRanges.Count == 0) return;
        var pos = e.GetPosition(_textBlock);
        var hit = _textBlock.TextLayout?.HitTestPoint(pos);
        if (hit == null) return;
        var charPos = hit.Value.TextPosition;
        var overUrl = false;
        foreach (var (start, end, _) in _urlRanges)
        {
            if (charPos >= start && charPos < end) { overUrl = true; break; }
        }
        _textBlock.Cursor = new Cursor(overUrl ? StandardCursorType.Hand : StandardCursorType.Arrow);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SegmentsProperty)
            RebuildInlines();
    }

    private void RebuildInlines()
    {
        _textBlock.Inlines ??= new InlineCollection();
        _textBlock.Inlines.Clear();
        _urlRanges.Clear();

        var segments = Segments;
        if (segments == null) return;

        var charPos = 0;
        foreach (var seg in segments)
        {
            switch (seg)
            {
                case TextSegment ts:
                    _textBlock.Inlines.Add(new Run(ts.Text));
                    charPos += ts.Text.Length;
                    break;

                case RaritySegment rs:
                    _textBlock.Inlines.Add(new Run(rs.Text)
                    {
                        Foreground = GetRarityBrush(rs.Rarity)
                    });
                    charPos += rs.Text.Length;
                    break;

                case EmoticonSegment es when es.Image != null:
                    var img = new Image
                    {
                        Source = es.Image,
                        Width = 18,
                        Height = 18,
                        Stretch = Stretch.Uniform
                    };
                    _textBlock.Inlines.Add(new InlineUIContainer(img));
                    charPos += 1; // InlineUIContainer occupies one character (U+FFFC)
                    break;

                case EmoticonSegment es:
                    var emoticonText = $":{es.Code}:";
                    _textBlock.Inlines.Add(new Run(emoticonText));
                    charPos += emoticonText.Length;
                    break;

                case UrlSegment us:
                    var start = charPos;
                    _textBlock.Inlines.Add(new Run(us.Url)
                    {
                        Foreground = new SolidColorBrush(Color.Parse("#3a90d6")),
                        TextDecorations = TextDecorations.Underline,
                    });
                    _urlRanges.Add((start, start + us.Url.Length, us.Url));
                    charPos += us.Url.Length;
                    break;
            }
        }
    }

    private static readonly Dictionary<string, IBrush> s_rarityBrushes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["common"]    = new SolidColorBrush(Color.Parse("#b0c3d9")),
            ["uncommon"]  = new SolidColorBrush(Color.Parse("#5e98d9")),
            ["rare"]      = new SolidColorBrush(Color.Parse("#4b69ff")),
            ["mythical"]  = new SolidColorBrush(Color.Parse("#8847ff")),
            ["legendary"] = new SolidColorBrush(Color.Parse("#d32ce6")),
            ["immortal"]  = new SolidColorBrush(Color.Parse("#e4ae39")),
            ["arcana"]    = new SolidColorBrush(Color.Parse("#ade55c")),
            ["ancient"]   = new SolidColorBrush(Color.Parse("#eb4b4b"))
        };

    private static IBrush GetRarityBrush(string rarity) =>
        s_rarityBrushes.TryGetValue(rarity, out var b) ? b : Brushes.White;
}
