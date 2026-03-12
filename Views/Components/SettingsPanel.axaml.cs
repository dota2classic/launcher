using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace d2c_launcher.Views.Components;

public partial class SettingsPanel : UserControl
{
    /// <summary>
    /// Routed event fired when the user clicks "Изменить".
    /// The parent view handles the file picker dialog.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectDirectoryRequestedEvent =
        RoutedEvent.Register<SettingsPanel, RoutedEventArgs>(
            nameof(SelectDirectoryRequested), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs>? SelectDirectoryRequested
    {
        add => AddHandler(SelectDirectoryRequestedEvent, value);
        remove => RemoveHandler(SelectDirectoryRequestedEvent, value);
    }

    public SettingsPanel()
    {
        InitializeComponent();
    }

    private void OnSelectDirectoryClicked(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(SelectDirectoryRequestedEvent, this));
    }

    private void OnCameraDistanceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb)
            TopLevel.GetTopLevel(tb)?.FocusManager?.ClearFocus();
    }

    private void OnGameplayTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not TextBox)
            TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
    }
}
