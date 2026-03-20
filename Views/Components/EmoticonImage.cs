using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using d2c_launcher.Util;

namespace d2c_launcher.Views.Components;

/// <summary>
/// Displays an emoticon from raw bytes, automatically choosing between
/// <see cref="Avalonia.Labs.Gif.GifImage"/> for GIFs and <see cref="Image"/> for other formats (PNG, etc.).
/// </summary>
public class EmoticonImage : ContentControl
{
    public static readonly StyledProperty<byte[]?> BytesProperty =
        AvaloniaProperty.Register<EmoticonImage, byte[]?>(nameof(Bytes));

    public byte[]? Bytes
    {
        get => GetValue(BytesProperty);
        set => SetValue(BytesProperty, value);
    }

    // Holds the MemoryStream backing the active GifImage so it can be disposed on update/detach.
    private MemoryStream? _gifStream;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BytesProperty)
            UpdateContent();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _gifStream?.Dispose();
        _gifStream = null;
    }

    private void UpdateContent()
    {
        var bytes = Bytes;
        if (bytes is not { Length: > 0 })
        {
            Content = null;
            _gifStream?.Dispose();
            _gifStream = null;
            return;
        }

        if (IsGif(bytes))
        {
            try
            {
                var stream = new MemoryStream(bytes);
                Content = new Avalonia.Labs.Gif.GifImage
                {
                    Source = stream,
                    Stretch = Stretch.Uniform,
                };
                _gifStream?.Dispose();
                _gifStream = stream;
            }
            catch (Exception ex)
            {
                AppLog.Warn($"EmoticonImage: failed to load GIF: {ex.Message}");
                Content = null;
            }
        }
        else
        {
            try
            {
                using var ms = new MemoryStream(bytes);
                Content = new Image
                {
                    Source = new Bitmap(ms),
                    Stretch = Stretch.Uniform,
                };
            }
            catch (Exception ex)
            {
                AppLog.Warn($"EmoticonImage: failed to decode image: {ex.Message}");
                Content = null;
            }
        }
    }

    private static bool IsGif(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F';
}
