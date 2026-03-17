using Avalonia;
using Avalonia.Controls;

namespace d2c_launcher.Views.Components;

/// <summary>
/// 3-segment KDA bar: red=kills, grey=deaths, green=assists, proportional to K+D+A sum.
/// </summary>
public partial class KdaBar : UserControl
{
    public static readonly StyledProperty<double> KillsProperty =
        AvaloniaProperty.Register<KdaBar, double>(nameof(Kills));
    public static readonly StyledProperty<double> DeathsProperty =
        AvaloniaProperty.Register<KdaBar, double>(nameof(Deaths));
    public static readonly StyledProperty<double> AssistsProperty =
        AvaloniaProperty.Register<KdaBar, double>(nameof(Assists));

    public double Kills   { get => GetValue(KillsProperty);   set => SetValue(KillsProperty, value); }
    public double Deaths  { get => GetValue(DeathsProperty);  set => SetValue(DeathsProperty, value); }
    public double Assists { get => GetValue(AssistsProperty); set => SetValue(AssistsProperty, value); }

    public KdaBar() => InitializeComponent();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KillsProperty ||
            change.Property == DeathsProperty ||
            change.Property == AssistsProperty ||
            change.Property == BoundsProperty)
            UpdateSegments();
    }

    private void UpdateSegments()
    {
        if (KillsRect == null) return;
        var totalWidth = Bounds.Width;
        if (totalWidth <= 0) return;

        var sum = Kills + Deaths + Assists;
        if (sum <= 0)
        {
            KillsRect.Width = 0; DeathsRect.Width = 0; AssistsRect.Width = 0;
            return;
        }

        var kw = Kills  / sum * totalWidth;
        var dw = Deaths / sum * totalWidth;
        var aw = Assists / sum * totalWidth;

        KillsRect.Width   = kw;
        DeathsRect.Margin = new Thickness(kw, 0, 0, 0);
        DeathsRect.Width  = dw;
        AssistsRect.Margin = new Thickness(kw + dw, 0, 0, 0);
        AssistsRect.Width  = aw;
    }
}
