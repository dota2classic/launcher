using System;
using Avalonia.Controls;
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
}
