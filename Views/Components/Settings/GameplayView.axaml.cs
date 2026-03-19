using Avalonia.Controls;
using Avalonia.Input;

namespace d2c_launcher.Views.Components.Settings;

public partial class GameplayView : UserControl
{
    public GameplayView()
    {
        InitializeComponent();
    }

    private void OnCameraDistanceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb)
            TopLevel.GetTopLevel(tb)?.FocusManager?.ClearFocus();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not TextBox)
            TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
    }
}
