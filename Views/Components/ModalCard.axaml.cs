using Avalonia;
using Avalonia.Controls;

namespace d2c_launcher.Views.Components;

public partial class ModalCard : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ModalCard, string?>(nameof(Title));

    public static readonly StyledProperty<double> FixedWidthProperty =
        AvaloniaProperty.Register<ModalCard, double>(nameof(FixedWidth), defaultValue: 520);

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public double FixedWidth
    {
        get => GetValue(FixedWidthProperty);
        set => SetValue(FixedWidthProperty, value);
    }

    public ModalCard()
    {
        InitializeComponent();
    }
}
