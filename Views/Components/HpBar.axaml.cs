using Avalonia;
using Avalonia.Controls;

namespace d2c_launcher.Views.Components;

public partial class HpBar : UserControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<HpBar, double>(nameof(Value));

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public HpBar() => InitializeComponent();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == BoundsProperty)
            UpdateFill();
    }

    private void UpdateFill()
    {
        if (FillRect == null) return;
        var totalWidth = Bounds.Width;
        FillRect.Width = totalWidth <= 0 ? 0 : Value / 100.0 * totalWidth;
    }
}
