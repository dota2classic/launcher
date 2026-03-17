using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace d2c_launcher.Views.Components;

/// <summary>
/// Renders the Dota minimap background with animated hero icons overlaid.
/// Set <see cref="UseSmallIcons"/> = true for the sidebar card (64px canvas icons, SmallHeroMargin).
/// Default (false) is the detail-panel minimap (28px canvas icons, HeroMargin).
/// </summary>
public partial class MinimapPanel : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<MinimapPanel, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<bool> UseSmallIconsProperty =
        AvaloniaProperty.Register<MinimapPanel, bool>(nameof(UseSmallIcons));

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public bool UseSmallIcons
    {
        get => GetValue(UseSmallIconsProperty);
        set => SetValue(UseSmallIconsProperty, value);
    }

    public MinimapPanel() => InitializeComponent();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
            HeroesControl.ItemsSource = ItemsSource;
        else if (change.Property == UseSmallIconsProperty)
            RefreshTemplate();
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        HeroesControl.ItemsSource = ItemsSource;
        RefreshTemplate();
    }

    private void RefreshTemplate()
    {
        if (HeroesControl == null) return;
        var key = UseSmallIcons ? "SmallIconTemplate" : "LargeIconTemplate";
        HeroesControl.ItemTemplate = (IDataTemplate)Resources[key]!;
    }
}
