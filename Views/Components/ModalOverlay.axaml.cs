using Avalonia;
using Avalonia.Controls;
using System.Windows.Input;

namespace d2c_launcher.Views.Components;

public partial class ModalOverlay : UserControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ModalOverlay, bool>(nameof(IsOpen));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ModalOverlay, string?>(nameof(Title));

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<ModalOverlay, ICommand?>(nameof(CloseCommand));

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public ModalOverlay()
    {
        InitializeComponent();
    }

    private void OnOverlayPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (CloseCommand?.CanExecute(null) == true)
            CloseCommand.Execute(null);
        e.Handled = true;
    }

    private void OnDialogPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
