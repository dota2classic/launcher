using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace d2c_launcher.Views.Components;

public partial class PartyPanel : UserControl
{
    public PartyPanel()
    {
        InitializeComponent();
    }

    // Bubble the invite click up to the parent view
    public static readonly RoutedEvent<RoutedEventArgs> InviteClickedEvent =
        RoutedEvent.Register<PartyPanel, RoutedEventArgs>(nameof(InviteClicked), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs> InviteClicked
    {
        add => AddHandler(InviteClickedEvent, value);
        remove => RemoveHandler(InviteClickedEvent, value);
    }

    private void OnInvitePlayerClicked(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(InviteClickedEvent));
    }
}
