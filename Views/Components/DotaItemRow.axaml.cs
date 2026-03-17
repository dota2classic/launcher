using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace d2c_launcher.Views.Components;

/// <summary>
/// Renders a row of up to 6 Dota item icons.
/// Bind <see cref="ItemUrls"/> to an <c>IReadOnlyList&lt;string?&gt;</c> of item image URLs.
/// </summary>
public partial class DotaItemRow : UserControl
{
    public static readonly StyledProperty<IReadOnlyList<string?>?> ItemUrlsProperty =
        AvaloniaProperty.Register<DotaItemRow, IReadOnlyList<string?>?>(nameof(ItemUrls));

    public static readonly StyledProperty<double> ItemWidthProperty =
        AvaloniaProperty.Register<DotaItemRow, double>(nameof(ItemWidth), defaultValue: 32);

    public static readonly StyledProperty<double> ItemHeightProperty =
        AvaloniaProperty.Register<DotaItemRow, double>(nameof(ItemHeight), defaultValue: 22);

    public IReadOnlyList<string?>? ItemUrls
    {
        get => GetValue(ItemUrlsProperty);
        set => SetValue(ItemUrlsProperty, value);
    }

    public double ItemWidth
    {
        get => GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public DotaItemRow()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemUrlsProperty)
            UpdateUrls();
        else if (change.Property == ItemWidthProperty || change.Property == ItemHeightProperty)
            UpdateSizes();
    }

    // Builds the 6 icon slots on first load (when ItemUrls is first set).
    // Subsequent calls only update the ItemUrl on existing icons.
    private void UpdateUrls()
    {
        if (ItemsPanel == null) return;
        var urls = ItemUrls;
        int count = urls?.Count ?? 0;

        if (ItemsPanel.Children.Count != 6)
        {
            ItemsPanel.Children.Clear();
            for (int i = 0; i < 6; i++)
                ItemsPanel.Children.Add(new DotaItemIcon { Width = ItemWidth, Height = ItemHeight });
        }

        for (int i = 0; i < 6; i++)
            ((DotaItemIcon)ItemsPanel.Children[i]).ItemUrl = i < count ? urls![i] : null;
    }

    private void UpdateSizes()
    {
        if (ItemsPanel == null) return;
        foreach (DotaItemIcon icon in ItemsPanel.Children)
        {
            icon.Width = ItemWidth;
            icon.Height = ItemHeight;
        }
    }
}
