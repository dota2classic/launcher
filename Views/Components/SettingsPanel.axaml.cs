using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace d2c_launcher.Views.Components;

public partial class SettingsPanel : UserControl
{
    /// <summary>
    /// Routed event fired when the user clicks "Изменить директорию".
    /// The parent view handles the file picker dialog.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectDirectoryRequestedEvent =
        RoutedEvent.Register<SettingsPanel, RoutedEventArgs>(
            nameof(SelectDirectoryRequested), RoutingStrategies.Bubble);

    /// <summary>
    /// Routed event fired when the user clicks "Закрыть".
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> CloseRequestedEvent =
        RoutedEvent.Register<SettingsPanel, RoutedEventArgs>(
            nameof(CloseRequested), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs>? SelectDirectoryRequested
    {
        add => AddHandler(SelectDirectoryRequestedEvent, value);
        remove => RemoveHandler(SelectDirectoryRequestedEvent, value);
    }

    public event EventHandler<RoutedEventArgs>? CloseRequested
    {
        add => AddHandler(CloseRequestedEvent, value);
        remove => RemoveHandler(CloseRequestedEvent, value);
    }

    public SettingsPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Sets the displayed game directory path. Called by the parent view.
    /// </summary>
    public void SetGameDirectory(string? path)
    {
        GameDirectoryText.Text = path ?? "Не указано";
    }

    private void OnSelectDirectoryClicked(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(SelectDirectoryRequestedEvent, this));
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(CloseRequestedEvent, this));
    }
}
