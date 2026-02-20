using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace d2c_launcher.Views.Components;

public partial class QueueButton : UserControl
{
    public QueueButton()
    {
        InitializeComponent();
    }

    public static readonly RoutedEvent<RoutedEventArgs> SearchClickedEvent =
        RoutedEvent.Register<QueueButton, RoutedEventArgs>(nameof(SearchClicked), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs> SearchClicked
    {
        add => AddHandler(SearchClickedEvent, value);
        remove => RemoveHandler(SearchClickedEvent, value);
    }

    private void OnQueueButtonClicked(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(SearchClickedEvent));
    }
}
