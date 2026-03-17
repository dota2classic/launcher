using Avalonia;
using Avalonia.Controls;
using AsyncImageLoader;

namespace d2c_launcher.Views.Components;

/// <summary>
/// Displays a single Dota item image. Set <see cref="ItemUrl"/> to the image URL,
/// or leave null/empty to show an empty slot placeholder.
/// </summary>
public partial class DotaItemIcon : UserControl
{
    public static readonly StyledProperty<string?> ItemUrlProperty =
        AvaloniaProperty.Register<DotaItemIcon, string?>(nameof(ItemUrl));

    public string? ItemUrl
    {
        get => GetValue(ItemUrlProperty);
        set => SetValue(ItemUrlProperty, value);
    }

    public DotaItemIcon()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemUrlProperty)
            UpdateImage(change.GetNewValue<string?>());
    }

    private void UpdateImage(string? url)
    {
        if (ItemImage == null) return;
        ItemImage.Source = string.IsNullOrEmpty(url) ? null : url;
        ItemImage.IsVisible = !string.IsNullOrEmpty(url);
    }
}
