using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using d2c_launcher.Models;
using d2c_launcher.Util;
using d2c_launcher.ViewModels;
using d2c_launcher.Views;
using d2c_launcher.Views.Components;

namespace d2c_launcher.Preview;

public static class PreviewRegistry
{
    private static readonly Dictionary<string, Func<(Control View, object? ViewModel)>> Registry =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["LauncherHeader"] = () =>
            {
                var stub = new StubQueueSocketService();
                var api = new StubBackendApiService();
                var vm = new MainLauncherViewModel(
                    new d2c_launcher.Integration.SteamManager(),
                    new StubSettingsStorage(),
                    new StubGameLaunchSettingsStorage(),
                    new StubCvarSettingsProvider(),
                    new StubVideoSettingsProvider(),
                    api,
                    stub,
                    new StubContentRegistryService(),
                    new StubChatViewModelFactory(),
                    new StubWindowService());
                var view = new LauncherHeader { Width = 900, Height = 48, DataContext = vm };
                return (view, null);
            },
            ["ProfilePanel"] = () =>
            {
                var api = new StubBackendApiService();
                var vm = new ProfileViewModel(api);
                _ = vm.LoadAsync("76561198000000001");
                return (new ProfilePanel { Width = 900, Height = 600 }, vm);
            },
            ["PartyPanel"] = () =>
            {
                var stub = new StubQueueSocketService();
                var vm = new PartyViewModel(stub, new StubBackendApiService());
                vm.PartyMembers.Add(new d2c_launcher.Models.PartyMemberView("1", "Player One", null));
                vm.PartyMembers.Add(new d2c_launcher.Models.PartyMemberView("2", "Player Two", null));
                return (new PartyPanel(), vm);
            },
            ["QueueButton"] = () =>
            {
                var vm = new QueueViewModel(new StubQueueSocketService(), new StubBackendApiService());
                vm.IsSearching = true;
                vm.SetEnterQueueAt(DateTimeOffset.UtcNow);
                vm.SetQueuedModeNames(new[] { "Против ботов" });
                vm.UpdateQueueButtonState();
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
            ["AcceptGameModal1"] = () =>
            {
                var vm = new RoomViewModel(new StubQueueSocketService(), new StubBackendApiService());
                vm.RoomPlayers.Add(new RoomPlayerView("1", "SoloPlayer", null, d2c_launcher.Services.ReadyState.Pending));
                vm.IsAcceptGameModalOpen = true;
                var host = new Panel { Width = 900, Height = 400, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1a1f26")) };
                host.Children.Add(new AcceptGameModal { DataContext = vm });
                return (host, null);
            },
            ["AcceptGameModal2"] = () =>
            {
                var vm = new RoomViewModel(new StubQueueSocketService(), new StubBackendApiService());
                vm.RoomPlayers.Add(new RoomPlayerView("1", "PlayerOne", null, d2c_launcher.Services.ReadyState.Ready));
                vm.RoomPlayers.Add(new RoomPlayerView("2", "PlayerTwo", null, d2c_launcher.Services.ReadyState.Pending));
                vm.IsAcceptGameModalOpen = true;
                var host = new Panel { Width = 900, Height = 400, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1a1f26")) };
                host.Children.Add(new AcceptGameModal { DataContext = vm });
                return (host, null);
            },
            ["AcceptGameModal5"] = () =>
            {
                var vm = new RoomViewModel(new StubQueueSocketService(), new StubBackendApiService());
                vm.RoomPlayers.Add(new RoomPlayerView("1", "PlayerOne",   null, d2c_launcher.Services.ReadyState.Ready));
                vm.RoomPlayers.Add(new RoomPlayerView("2", "PlayerTwo",   null, d2c_launcher.Services.ReadyState.Ready));
                vm.RoomPlayers.Add(new RoomPlayerView("3", "PlayerThree", null, d2c_launcher.Services.ReadyState.Pending));
                vm.RoomPlayers.Add(new RoomPlayerView("4", "PlayerFour",  null, d2c_launcher.Services.ReadyState.Decline));
                vm.RoomPlayers.Add(new RoomPlayerView("5", "PlayerFive",  null, d2c_launcher.Services.ReadyState.Pending));
                vm.IsAcceptGameModalOpen = true;
                var host = new Panel { Width = 900, Height = 400, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1a1f26")) };
                host.Children.Add(new AcceptGameModal { DataContext = vm });
                return (host, null);
            },
            ["AcceptGameModal10"] = () =>
            {
                var vm = new RoomViewModel(new StubQueueSocketService(), new StubBackendApiService());
                for (int i = 1; i <= 10; i++)
                    vm.RoomPlayers.Add(new RoomPlayerView(i.ToString(), $"Player{i}", null,
                        i % 3 == 0 ? d2c_launcher.Services.ReadyState.Ready : d2c_launcher.Services.ReadyState.Pending));
                vm.IsAcceptGameModalOpen = true;
                var host = new Panel { Width = 1200, Height = 400, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1a1f26")) };
                host.Children.Add(new AcceptGameModal { DataContext = vm });
                return (host, null);
            },
            ["NotificationArea"] = () =>
            {
                var vm = new NotificationAreaViewModel(new StubQueueSocketService());
                return (new NotificationArea(), vm);
            },
            ["Loading"] = () =>
                (new LoadingView(), new LoadingViewModel()),
            ["LaunchSteamFirst"] = () =>
                (new LaunchSteamFirstView(), new LaunchSteamFirstViewModel()),
            ["SelectGame"] = () =>
                (new SelectGameView(), new SelectGameViewModel(new StubContentRegistryService())),
            ["GameDownload"] = () =>
            {
                var vm = new GameDownloadViewModel(
                    new StubContentRegistryService(),
                    new StubLocalManifestService(),
                    new StubManifestDiffService(),
                    new StubGameDownloadService(),
                    new d2c_launcher.Services.RedistInstallService())
                {
                    GameDirectory = @"C:\fake\dotaclassic",
                    StatusText = "Загрузка (142/2381 файлов)",
                    DetailsText = "dota/bin/win64/engine.dll\n12.3 МБ / 24.1 ГБ  1.8 МБ/с  ~3ч 42м",
                    ProgressValue = 42,
                    IsIndeterminate = false,
                };
                return (new GameDownloadView(), vm);
            },
            ["GameDownloadError"] = () =>
            {
                var vm = new GameDownloadViewModel(
                    new StubContentRegistryService(),
                    new StubLocalManifestService(),
                    new StubManifestDiffService(),
                    new StubGameDownloadService(),
                    new d2c_launcher.Services.RedistInstallService())
                {
                    GameDirectory = @"C:\fake\dotaclassic",
                    StatusText = "Ошибка загрузки",
                    ErrorText = "Ошибка подключения к серверу обновлений.\nПроверьте интернет-соединение и попробуйте снова.",
                    ProgressValue = 47,
                    IsIndeterminate = false,
                    HasError = true,
                };
                return (new GameDownloadView(), vm);
            },
            ["SettingsPanel"] = () =>
            {
                var launchStorage = new StubGameLaunchSettingsStorage();
                var cvarProvider = new StubCvarSettingsProvider();
                var settingsStorage = new StubSettingsStorage();
                var videoProvider = new StubVideoSettingsProvider();
                var vm = new SettingsViewModel(launchStorage, cvarProvider, settingsStorage, videoProvider, new StubContentRegistryService());
                vm.RefreshGameDirectory();
                return (new SettingsPanelPreviewControl(), vm);
            },
            ["ChatPanel"] = () =>
            {
                var stub = new StubBackendApiService();
                var vm = new ChatViewModel("preview-thread", stub, new StubHttpImageService(), new StubEmoticonService(), new StubQueueSocketService(), new StubWindowService());
                _ = vm.StartAsync();
                var view = new ChatPanel { DataContext = vm, Width = 620, Height = 520 };
                return (view, null);
            },
            ["RichMessage"] = () =>
            {
                var lines = new[]
                {
                    "простой текст",
                    "играю сносно https://dotaclassic.ru/players/198768255 але",
                    "текст перед https://dotaclassic.ru/players/198768255",
                    "https://dotaclassic.ru/players/198768255 текст после",
                    "две ссылки https://dotaclassic.ru/ и https://dotaclassic.ru/players/198768255 конец",
                };
                var stack = new StackPanel
                {
                    Spacing = 6,
                    Margin = new Thickness(16),
                    Background = new SolidColorBrush(Color.Parse("#060708")),
                };
                foreach (var line in lines)
                {
                    stack.Children.Add(new RichMessageBlock
                    {
                        Segments = RichMessageParser.Parse(line),
                    });
                }
                var host = new Border
                {
                    Width = 500,
                    Background = new SolidColorBrush(Color.Parse("#060708")),
                    Child = stack,
                };
                return (host, null);
            },
            ["InviteModal"] = () =>
            {
                var stub = new StubQueueSocketService();
                var vm = new PartyViewModel(stub, new StubBackendApiService());
                vm.InviteCandidates = new ObservableCollection<InviteCandidateView>([
                    new InviteCandidateView("1", "♥Gryst♥",             "G",  isOnline: true),
                    new InviteCandidateView("2", "rampage",              "R",  isOnline: true),
                    new InviteCandidateView("3", "Богдан",               "Б",  isOnline: true),
                    new InviteCandidateView("4", "просто хлеб",          "П",  isOnline: true),
                    new InviteCandidateView("5", "STEALTH-ТРАМБАi",      "S",  isOnline: true),
                    new InviteCandidateView("6", "egor_lib",             "E",  isOnline: false),
                    new InviteCandidateView("7", "Buyback из Алабуги",   "B",  isOnline: true),
                    new InviteCandidateView("8", "divine orb",           "D",  isOnline: false),
                ]);
                vm.IsInviteModalOpen = true;

                // Wrap the ModalCard in a fixed-size dark panel to simulate the overlay context
                var hostPanel = new Panel
                {
                    Width = 600,
                    Height = 520,
                    Background = new SolidColorBrush(Color.Parse("#90000000")),
                };
                var view = new InviteModalPreviewControl { DataContext = vm };
                hostPanel.Children.Add(view);
                return (hostPanel, null);
            },
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
