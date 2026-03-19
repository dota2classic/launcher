using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BytesProperty)
            UpdateContent();
    }

    private void UpdateContent()
    {
        var bytes = Bytes;
        if (bytes is not { Length: > 0 })
        {
            Content = null;
            return;
        }

        if (IsGif(bytes))
        {
            Content = new Avalonia.Labs.Gif.GifImage
            {
                Source = new MemoryStream(bytes),
                Stretch = Stretch.Uniform,
            };
        }
        else
        {
            try
            {
                Content = new Image
                {
                    Source = new Bitmap(new MemoryStream(bytes)),
                    Stretch = Stretch.Uniform,
                };
            }
            catch
            {
                Content = null;
            }
        }
    }

    private static bool IsGif(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F';
}
