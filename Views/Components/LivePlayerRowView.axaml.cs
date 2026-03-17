using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace d2c_launcher.Views.Components;

/// <summary>
/// Renders a single live-match player row.
/// Set <see cref="MirrorLayout"/> = true for Dire (columns flipped: KDA | Name | Hero).
/// Default (false) is Radiant layout: Hero | Name | KDA.
/// </summary>
public partial class LivePlayerRowView : UserControl
{
    public static readonly StyledProperty<bool> MirrorLayoutProperty =
        AvaloniaProperty.Register<LivePlayerRowView, bool>(nameof(MirrorLayout));

    public bool MirrorLayout
    {
        get => GetValue(MirrorLayoutProperty);
        set => SetValue(MirrorLayoutProperty, value);
    }

    public LivePlayerRowView() => InitializeComponent();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MirrorLayoutProperty)
            ApplyLayout((bool)change.NewValue!);
    }

    private void ApplyLayout(bool mirror)
    {
        if (HeroPanel == null) return; // not yet loaded

        if (mirror)
        {
            // Dire: KDA(0) | Name(1) | Hero(2)
            Grid.SetColumn(KdaPanel, 0);
            KdaPanel.HorizontalAlignment = HorizontalAlignment.Left;
            KdaPanel.Margin = new Thickness(0, 0, 8, 0);
            (KdaPanel.Children[0] as Avalonia.Controls.TextBlock)!.HorizontalAlignment = HorizontalAlignment.Left;

            Grid.SetColumn(NameBlock, 1);
            NameBlock.HorizontalAlignment = HorizontalAlignment.Right;

            Grid.SetColumn(HeroPanel, 2);
            HeroPanel.Margin = new Thickness(8, 0, 0, 0);
            (HeroPanel.Children[1] as Border)!.HorizontalAlignment = HorizontalAlignment.Right;

            ItemRow.HorizontalAlignment = HorizontalAlignment.Right;
        }
        else
        {
            // Radiant: Hero(0) | Name(1) | KDA(2)
            Grid.SetColumn(HeroPanel, 0);
            HeroPanel.Margin = new Thickness(0, 0, 8, 0);
            (HeroPanel.Children[1] as Border)!.HorizontalAlignment = HorizontalAlignment.Left;

            Grid.SetColumn(NameBlock, 1);
            NameBlock.HorizontalAlignment = HorizontalAlignment.Left;

            Grid.SetColumn(KdaPanel, 2);
            KdaPanel.HorizontalAlignment = HorizontalAlignment.Right;
            KdaPanel.Margin = new Thickness(8, 0, 0, 0);
            (KdaPanel.Children[0] as Avalonia.Controls.TextBlock)!.HorizontalAlignment = HorizontalAlignment.Right;

            ItemRow.HorizontalAlignment = HorizontalAlignment.Left;
        }
    }
}
