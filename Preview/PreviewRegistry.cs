using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using d2c_launcher.ViewModels;
using d2c_launcher.Views;
using d2c_launcher.Views.Components;

namespace d2c_launcher.Preview;

public static class PreviewRegistry
{
    private static readonly Dictionary<string, Func<(Control View, object? ViewModel)>> Registry =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PartyPanel"] = () =>
            {
                var stub = new StubQueueSocketService();
                var vm = new PartyViewModel(stub, new StubBackendApiService());
                vm.PartyMembers.Add(new d2c_launcher.Models.PartyMemberView("1", "Player One", null, null));
                vm.PartyMembers.Add(new d2c_launcher.Models.PartyMemberView("2", "Player Two", null, null));
                return (new PartyPanel(), vm);
            },
            ["QueueButton"] = () =>
            {
                var vm = new QueueViewModel(new StubQueueSocketService(), new StubBackendApiService());
                return (new QueueButton(), vm);
            },
            ["GameSearchPanel"] = () =>
            {
                var vm = new QueueViewModel(new StubQueueSocketService(), new StubBackendApiService());
                return (new GameSearchPanel(), vm);
            },
            ["AcceptGameModal"] = () =>
            {
                var vm = new RoomViewModel(new StubQueueSocketService(), new StubBackendApiService());
                return (new AcceptGameModal(), vm);
            },
            ["NotificationArea"] = () =>
            {
                var vm = new NotificationAreaViewModel(new StubBackendApiService(), new StubQueueSocketService());
                return (new NotificationArea(), vm);
            },
            ["LaunchSteamFirst"] = () =>
                (new LaunchSteamFirstView(), new LaunchSteamFirstViewModel()),
            ["SelectGame"] = () =>
                (new SelectGameView(), new SelectGameViewModel()),
        };

    public static IEnumerable<string> AvailableNames => Registry.Keys;

    public static (Control View, object? ViewModel) Create(string name)
    {
        if (Registry.TryGetValue(name, out var factory))
            return factory();

        var list = string.Join("\n  ", Registry.Keys);
        var help = string.IsNullOrWhiteSpace(name)
            ? $"Usage: d2c-launcher.exe --preview <ComponentName>\n\nAvailable:\n  {list}"
            : $"Unknown component: '{name}'\n\nAvailable:\n  {list}";

        return (new TextBlock
        {
            Text = help,
            Foreground = Avalonia.Media.Brushes.White,
            Margin = new Thickness(4),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        }, null);
    }
}
