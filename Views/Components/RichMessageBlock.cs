using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using d2c_launcher.Models;
using d2c_launcher.Services;

namespace d2c_launcher.Views.Components;

public sealed class PlayerLinkClickedEventArgs : RoutedEventArgs
{
    public string Steam32Id { get; }
    public PlayerLinkClickedEventArgs(RoutedEvent routedEvent, object source, string steam32Id)
        : base(routedEvent, source) => Steam32Id = steam32Id;
}

/// <summary>
/// Renders a list of <see cref="RichSegment"/> items as mixed inline content
/// (plain text, colored rarity text, emoticon images, clickable URLs) inside a
/// <see cref="TextBlock"/>, preserving proper word-wrap behavior.
/// </summary>
public class RichMessageBlock : UserControl
{
    private static readonly HttpClient s_http = new();

    public static readonly RoutedEvent<PlayerLinkClickedEventArgs> PlayerLinkClickedEvent =
        RoutedEvent.Register<RichMessageBlock, PlayerLinkClickedEventArgs>(
            nameof(PlayerLinkClicked), RoutingStrategies.Bubble);

    public event EventHandler<PlayerLinkClickedEventArgs> PlayerLinkClicked
    {
        add => AddHandler(PlayerLinkClickedEvent, value);
        remove => RemoveHandler(PlayerLinkClickedEvent, value);
    }

    private readonly TextBlock _textBlock;
    // Maps character ranges to URLs for click detection.
    private readonly List<(int Start, int End, string Url)> _urlRanges = new();
    // Maps character ranges to steam32 IDs for player link click detection.
    private readonly List<(int Start, int End, string Steam32Id)> _playerLinkRanges = new();
    // Active PropertyChanged subscriptions on PlayerNameViewModels — cleared on each RebuildInlines.
    private readonly List<(PlayerNameViewModel Vm, PropertyChangedEventHandler Handler)> _nameSubscriptions = new();

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

        this.ResourcesChanged += (_, _) => ApplyFontSize();
        ApplyFontSize();
    }

    private void OnTextBlockPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_urlRanges.Count == 0 && _playerLinkRanges.Count == 0) return;
        var pos = e.GetPosition(_textBlock);
        var hit = _textBlock.TextLayout?.HitTestPoint(pos);
        if (hit == null) return;
        var charPos = hit.Value.TextPosition;

        foreach (var (start, end, steam32Id) in _playerLinkRanges)
        {
            if (charPos >= start && charPos < end)
            {
                RaiseEvent(new PlayerLinkClickedEventArgs(PlayerLinkClickedEvent, this, steam32Id));
                return;
            }
        }

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
        if (_urlRanges.Count == 0 && _playerLinkRanges.Count == 0) return;
        var pos = e.GetPosition(_textBlock);
        var hit = _textBlock.TextLayout?.HitTestPoint(pos);
        if (hit == null) return;
        var charPos = hit.Value.TextPosition;
        var overLink = false;
        foreach (var (start, end, _) in _playerLinkRanges)
        {
            if (charPos >= start && charPos < end) { overLink = true; break; }
        }
        if (!overLink)
        {
            foreach (var (start, end, _) in _urlRanges)
            {
                if (charPos >= start && charPos < end) { overLink = true; break; }
            }
        }
        _textBlock.Cursor = new Cursor(overLink ? StandardCursorType.Hand : StandardCursorType.Arrow);
    }

    private void ApplyFontSize()
    {
        if (this.TryFindResource("FontSizeBase", out var res) && res is double d)
            _textBlock.FontSize = d;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SegmentsProperty)
            RebuildInlines();
    }

    private void RebuildInlines()
    {
        foreach (var (vm, handler) in _nameSubscriptions)
            vm.PropertyChanged -= handler;
        _nameSubscriptions.Clear();

        _textBlock.Inlines ??= new InlineCollection();
        _textBlock.Inlines.Clear();
        _urlRanges.Clear();
        _playerLinkRanges.Clear();

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

                case EmoticonSegment es:
                    var emoticonCtrl = new EmoticonImage { Bytes = es.Bytes, Width = 20, Height = 20 };
                    _textBlock.Inlines.Add(new InlineUIContainer(emoticonCtrl)
                    {
                        BaselineAlignment = BaselineAlignment.Center
                    });
                    charPos += 1; // InlineUIContainer occupies one character (U+FFFC)
                    break;

                case PlayerLinkSegment pls:
                    var plsStart = charPos;
                    var displayName = pls.NameViewModel.DisplayName;
                    _textBlock.Inlines.Add(new Run(displayName)
                    {
                        Foreground = new SolidColorBrush(Color.Parse("#3a90d6")),
                    });
                    _playerLinkRanges.Add((plsStart, plsStart + displayName.Length, pls.SteamId));
                    charPos += displayName.Length;

                    // When the name resolves, rebuild so the Run text and click ranges update.
                    PropertyChangedEventHandler nameHandler = (_, _) => RebuildInlines();
                    pls.NameViewModel.PropertyChanged += nameHandler;
                    _nameSubscriptions.Add((pls.NameViewModel, nameHandler));
                    break;

                case ImageSegment img:
                    var imageCtrl = new Image
                    {
                        Height = 150,
                        Stretch = Stretch.Uniform,
                        Margin = new Thickness(0, 4, 0, 4)
                    };
                    _textBlock.Inlines.Add(new InlineUIContainer(imageCtrl));
                    charPos += 1;
                    LoadImageAsync(img.Url, imageCtrl);
                    break;

                case UrlSegment us:
                    var start = charPos;
                    _textBlock.Inlines.Add(new Run(us.Url)
                    {
                        Foreground = new SolidColorBrush(Color.Parse("#3a90d6")),
                    });
                    _urlRanges.Add((start, start + us.Url.Length, us.Url));
                    charPos += us.Url.Length;
                    break;
            }
        }
    }

    private static async void LoadImageAsync(string url, Image target)
    {
        try
        {
            var bytes = await s_http.GetByteArrayAsync(url).ConfigureAwait(false);
            var bitmap = new Avalonia.Media.Imaging.Bitmap(new MemoryStream(bytes));
            Dispatcher.UIThread.Post(() => target.Source = bitmap);
        }
        catch { /* silently ignore failed image loads */ }
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
