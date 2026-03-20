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

// Renders a button frozen in its hover appearance for static preview screenshots.
// Uses LocalValue writes to the ContentPresenter (highest non-animation priority)
// so that both FluentTheme ControlTheme styles and Application.Styles are bypassed.
file sealed class SimHoverButton : Button
{
    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (e.NameScope.Find<Avalonia.Controls.Presenters.ContentPresenter>("PART_ContentPresenter") is not { } cp)
            return;

        if (Classes.Contains("PrimaryButton"))
        {
            cp.Background = new Avalonia.Media.LinearGradientBrush
            {
                StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                EndPoint   = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
                GradientStops =
                {
                    new Avalonia.Media.GradientStop(Avalonia.Media.Color.Parse("#2a6aba"), 0),
                    new Avalonia.Media.GradientStop(Avalonia.Media.Color.Parse("#4aa0e6"), 1),
                },
            };
        }
        else if (Classes.Contains("DangerButton"))
        {
            cp.Background      = new SolidColorBrush(Color.Parse("#2c1412"));
            cp.BorderBrush     = new SolidColorBrush(Color.Parse("#c23c2a"));
            cp.BorderThickness = new Avalonia.Thickness(1);
            cp.Foreground      = new SolidColorBrush(Color.Parse("#c23c2a"));
        }
    }
}

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
                    new StubSteamManager(),
                    new StubSettingsStorage(),
                    new StubGameLaunchSettingsStorage(),
                    new StubCvarSettingsProvider(),
                    new StubVideoSettingsProvider(),
                    api,
                    stub,
                    new StubContentRegistryService(),
                    new StubChatViewModelFactory(),
                    new StubWindowService(),
                    new StubSteamAuthApi(),
                    new StubUiDispatcher(),
                    new StubTriviaRepository());
                var view = new LauncherHeader { Width = 900, Height = 48, DataContext = vm };
                return (view, null);
            },
            ["LauncherHeaderPlay"] = () =>
            {
                var vm = new MainLauncherViewModel(
                    new StubSteamManager(), new StubSettingsStorage(),
                    new StubGameLaunchSettingsStorage(), new StubCvarSettingsProvider(),
                    new StubVideoSettingsProvider(), new StubBackendApiService(),
                    new StubQueueSocketService(), new StubContentRegistryService(),
                    new StubChatViewModelFactory(), new StubWindowService(), new StubSteamAuthApi(),
                    new StubUiDispatcher(), new StubTriviaRepository());
                // Play state: game not running
                vm.Launch.RunState = GameRunState.None;
                var stack = new StackPanel { Spacing = 2, Background = new SolidColorBrush(Color.Parse("#1a1f26")) };
                stack.Children.Add(new TextBlock { Text = "ИГРАТЬ (game not running)", Foreground = Brushes.Gray, FontSize = 10, Margin = new Thickness(8, 4, 0, 0) });
                stack.Children.Add(new LauncherHeader { Width = 900, Height = 48, DataContext = vm });
                return (stack, null);
            },
            ["LauncherHeaderStop"] = () =>
            {
                var vm = new MainLauncherViewModel(
                    new StubSteamManager(), new StubSettingsStorage(),
                    new StubGameLaunchSettingsStorage(), new StubCvarSettingsProvider(),
                    new StubVideoSettingsProvider(), new StubBackendApiService(),
                    new StubQueueSocketService(), new StubContentRegistryService(),
                    new StubChatViewModelFactory(), new StubWindowService(), new StubSteamAuthApi(),
                    new StubUiDispatcher(), new StubTriviaRepository());
                // Stop state: our game is running
                vm.Launch.RunState = GameRunState.OurGameRunning;
                var stack = new StackPanel { Spacing = 2, Background = new SolidColorBrush(Color.Parse("#1a1f26")) };
                stack.Children.Add(new TextBlock { Text = "СТОП (game running)", Foreground = Brushes.Gray, FontSize = 10, Margin = new Thickness(8, 4, 0, 0) });
                stack.Children.Add(new LauncherHeader { Width = 900, Height = 48, DataContext = vm });
                return (stack, null);
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
                var vm = new QueueViewModel(new StubQueueSocketService(), new StubBackendApiService(), new StubSettingsStorage(), new StubTriviaRepository());
                vm.IsSearching = true;
                vm.SetEnterQueueAt(DateTimeOffset.UtcNow);
                vm.SetQueuedModeNames(new[] { "Против ботов" });
                vm.UpdateQueueButtonState();
                return (new QueueButton(), vm);
            },
            ["QueueButtonSingle"] = () =>
            {
                var vm = new QueueViewModel(new StubQueueSocketService(), new StubBackendApiService(), new StubSettingsStorage(), new StubTriviaRepository());
                vm.UpdateQueueButtonState(); // default idle = ИГРАТЬ, height=52
                var btn = new QueueButton { DataContext = vm, Width = 360 };
                var host = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#FF00FF")), // magenta so we see bounds
                    Padding = new Thickness(20),
                    Child = btn,
                };
                return (host, null);
            },
            ["GameSearchPanel"] = () =>
            {
                var vm = new QueueViewModel(new StubQueueSocketService(), new StubBackendApiService(), new StubSettingsStorage(), new StubTriviaRepository());
                return (new GameSearchPanel(), vm);
            },
            ["GameSearchPanelTrivia"] = () =>
            {
                var vm = new QueueViewModel(new StubQueueSocketService(), new StubBackendApiService(), new StubSettingsStorage(), new StubTriviaRepository());
                vm.IsSearching = true;
                _ = vm.Trivia.StartAsync();
                var host = new Border
                {
                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1a1f26")),
                    Width = 360,
                    Child = new GameSearchPanel { DataContext = vm },
                };
                return (host, null);
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
            ["PleaseGoQueue"] = () =>
            {
                var vm = new NotificationAreaViewModel(new StubQueueSocketService());
                vm.AddGoQueueToast("🎮 Давай сыграем в Обычная 5x5!", "В поиске уже 4 игрока!");
                var host = new Panel
                {
                    Width = 900,
                    Height = 400,
                    Background = new SolidColorBrush(Color.Parse("#1a1f26")),
                };
                host.Children.Add(new NotificationArea { DataContext = vm });
                return (host, null);
            },
            ["AchievementToast"] = () =>
            {
                var api = new StubBackendApiService();
                var vm = new NotificationAreaViewModel(new StubQueueSocketService());
                vm.AddAchievementToast(new d2c_launcher.Api.NotificationDto
                {
                    Id = "preview-notif-1",
                    SteamId = "39734273",
                    Title = "Победа за 1 час против Techies",
                    Content = "Выиграйте игру продолжительностью более 1 часа против команды с Techies.",
                    NotificationType = d2c_launcher.Api.NotificationType.ACHIEVEMENT_COMPLETE,
                    EntityType = d2c_launcher.Api.NotificationDtoEntityType.ACHIEVEMENT,
                    EntityId = "0",
                    CreatedAt = "2026-01-01T00:00:00Z",
                    ExpiresAt = "2026-12-31T00:00:00Z",
                    Achievement = new d2c_launcher.Api.NotificationAchievementDto { Key = 0 },
                    Params = new object(),
                }, api);
                var host = new Panel
                {
                    Width = 900,
                    Height = 400,
                    Background = new SolidColorBrush(Color.Parse("#1a1f26")),
                };
                host.Children.Add(new NotificationArea { DataContext = vm });
                return (host, null);
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
            ["Minimap"] = () =>
            {
                var json = System.IO.File.ReadAllText(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "Preview", "livematch.json"));
                var dto = System.Text.Json.JsonSerializer.Deserialize<d2c_launcher.Api.LiveMatchDto>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                var card = new d2c_launcher.ViewModels.LiveMatchCardViewModel((int)dto.MatchId);
                card.UpdateFrom(dto, "Против ботов");
                var minimap = new d2c_launcher.Views.Components.MinimapPanel
                {
                    ItemsSource = card.Heroes,
                    BuildingsSource = card.Buildings,
                    UseSmallIcons = false,
                    Width = 320,
                    Height = 320,
                };
                return (minimap, null);
            },
            ["LivePanel"] = () =>
            {
                var json = System.IO.File.ReadAllText(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "Preview", "livematch.json"));
                var dto = System.Text.Json.JsonSerializer.Deserialize<d2c_launcher.Api.LiveMatchDto>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                var card = new d2c_launcher.ViewModels.LiveMatchCardViewModel((int)dto.MatchId);
                card.UpdateFrom(dto, "Против ботов");
                var vm = new d2c_launcher.ViewModels.LiveViewModel(new StubBackendApiService());
                vm.Matches.Add(card);
                vm.HasNoMatches = false;
                vm.IsLoading = false;
                vm.SelectedMatch = card;
                return (new d2c_launcher.Views.Components.LivePanel { Width = 1250, Height = 650 }, vm);
            },
            ["LivePlayerRowDead"] = () =>
            {
                var aliveSlot = new d2c_launcher.Api.MatchSlotInfo
                {
                    User = new d2c_launcher.Api.UserDTO { SteamId = "1", Name = "Tinker (alive)" },
                    Team = 2,
                    Connection = 1,
                    HeroData = new d2c_launcher.Api.PlayerInfo
                    {
                        Hero = "npc_dota_hero_tinker", Level = 25, Bot = false,
                        Kills = 5, Deaths = 2, Assists = 3,
                        Health = 2109, Max_health = 2212,
                        Respawn_time = -1,
                        Item0 = 1, Item1 = 178, Item2 = 119, Item3 = 220, Item4 = 204, Item5 = 108,
                    }
                };
                var deadSlot = new d2c_launcher.Api.MatchSlotInfo
                {
                    User = new d2c_launcher.Api.UserDTO { SteamId = "2", Name = "Axe (dead)" },
                    Team = 2,
                    Connection = 1,
                    HeroData = new d2c_launcher.Api.PlayerInfo
                    {
                        Hero = "npc_dota_hero_axe", Level = 18, Bot = false,
                        Kills = 3, Deaths = 7, Assists = 1,
                        Health = 0, Max_health = 2000,
                        Respawn_time = 42,
                        Item0 = 116, Item1 = 112, Item2 = 0, Item3 = 63, Item4 = 46, Item5 = 0,
                    }
                };
                var aliveVm = new d2c_launcher.ViewModels.LivePlayerRowViewModel(aliveSlot);
                var deadVm = new d2c_launcher.ViewModels.LivePlayerRowViewModel(deadSlot);

                var stack = new StackPanel
                {
                    Spacing = 4, Margin = new Thickness(16),
                    Background = new SolidColorBrush(Color.Parse("#1a1f26")),
                };
                stack.Children.Add(new d2c_launcher.Views.Components.LivePlayerRowView { DataContext = aliveVm, Width = 260 });
                stack.Children.Add(new d2c_launcher.Views.Components.LivePlayerRowView { DataContext = deadVm, Width = 260 });
                var host = new Border { Background = new SolidColorBrush(Color.Parse("#1a1f26")), Child = stack };
                return (host, null);
            },
            ["AbandonButtonConnect"] = () =>
            {
                // Single-line state: QueueButtonHeight = 52, abandon X visible
                var queueVm = new QueueViewModel(new StubQueueSocketService(), new StubBackendApiService(), new StubSettingsStorage(), new StubTriviaRepository());
                queueVm.UpdateQueueButtonState(); // default = ИГРАТЬ, height=52

                var queueBtn = new QueueButton { DataContext = queueVm };

                var abandonBtn = new Button
                {
                    Width = 52,
                    Height = queueVm.QueueButtonHeight,
                    Padding = new Thickness(0),
                    Margin = new Thickness(6, 0, 0, 0),
                    Background = new SolidColorBrush(Color.Parse("#1a0f0f")),
                    BorderBrush = new SolidColorBrush(Color.Parse("#C62626")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(0),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Content = new PathIcon
                    {
                        Data = Avalonia.Media.Geometry.Parse("M19,6.41L17.59,5L12,10.59L6.41,5L5,6.41L10.59,12L5,17.59L6.41,19L12,13.41L17.59,19L19,17.59L13.41,12L19,6.41Z"),
                        Foreground = new SolidColorBrush(Color.Parse("#C62626")),
                        Width = 16,
                        Height = 16,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    },
                };

                var grid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Margin = new Thickness(16),
                    Width = 360,
                };
                Grid.SetColumn(queueBtn, 0);
                Grid.SetColumn(abandonBtn, 1);
                grid.Children.Add(queueBtn);
                grid.Children.Add(abandonBtn);

                var host = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#1a1f26")),
                    Padding = new Thickness(16),
                    Child = grid,
                };
                return (host, null);
            },
            ["AbandonButtonSearching"] = () =>
            {
                // Searching state: QueueButtonHeight = 80, abandon X visible
                var queueVm = new QueueViewModel(new StubQueueSocketService(), new StubBackendApiService(), new StubSettingsStorage(), new StubTriviaRepository());
                queueVm.IsSearching = true;
                queueVm.SetEnterQueueAt(DateTimeOffset.UtcNow);
                queueVm.SetQueuedModeNames(new[] { "Против ботов" });
                queueVm.UpdateQueueButtonState();

                var queueBtn = new QueueButton { DataContext = queueVm };

                var abandonBtn = new Button
                {
                    Width = 52,
                    Height = queueVm.QueueButtonHeight,
                    Padding = new Thickness(0),
                    Margin = new Thickness(6, 0, 0, 0),
                    Background = new SolidColorBrush(Color.Parse("#1a0f0f")),
                    BorderBrush = new SolidColorBrush(Color.Parse("#C62626")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(0),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Content = new PathIcon
                    {
                        Data = Avalonia.Media.Geometry.Parse("M19,6.41L17.59,5L12,10.59L6.41,5L5,6.41L10.59,12L5,17.59L6.41,19L12,13.41L17.59,19L19,17.59L13.41,12L19,6.41Z"),
                        Foreground = new SolidColorBrush(Color.Parse("#C62626")),
                        Width = 16,
                        Height = 16,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    },
                };

                var grid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Margin = new Thickness(16),
                    Width = 360,
                };
                Grid.SetColumn(queueBtn, 0);
                Grid.SetColumn(abandonBtn, 1);
                grid.Children.Add(queueBtn);
                grid.Children.Add(abandonBtn);

                var host = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#1a1f26")),
                    Padding = new Thickness(16),
                    Child = grid,
                };
                return (host, null);
            },
            ["ButtonStates"] = () =>
            {
                static TextBlock Label(string text) => new TextBlock
                {
                    Text = text, Foreground = new SolidColorBrush(Color.Parse("#888")),
                    FontSize = 10, Margin = new Thickness(0, 8, 0, 4),
                };
                static Button MakeButton(string text, string[] classes, double pad = 28, bool hover = false)
                {
                    Button b = hover
                        ? new SimHoverButton { Content = text, Padding = new Avalonia.Thickness(pad, 10), CornerRadius = new Avalonia.CornerRadius(0) }
                        : new Button       { Content = text, Padding = new Avalonia.Thickness(pad, 10), CornerRadius = new Avalonia.CornerRadius(0) };
                    foreach (var c in classes) b.Classes.Add(c);
                    return b;
                }

                var col = new StackPanel { Spacing = 2, Margin = new Thickness(20), Width = 400 };

                col.Children.Add(Label("PrimaryButton (normal)"));
                col.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children =
                {
                    MakeButton("ПРИНЯТЬ", ["PrimaryButton"]),
                    MakeButton("ОТМЕНИТЬ", ["PrimaryButton"]),
                }});

                col.Children.Add(Label("PrimaryButton (hover)"));
                col.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children =
                {
                    MakeButton("ПРИНЯТЬ", ["PrimaryButton"], hover: true),
                    MakeButton("ОТМЕНИТЬ", ["PrimaryButton"], hover: true),
                }});

                col.Children.Add(Label("DangerButton (normal)"));
                col.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children =
                {
                    MakeButton("ОТКЛОНИТЬ", ["DangerButton"]),
                    MakeButton("ПОКИНУТЬ", ["DangerButton"]),
                }});

                col.Children.Add(Label("DangerButton (hover)"));
                col.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children =
                {
                    MakeButton("ОТКЛОНИТЬ", ["DangerButton"], hover: true),
                    MakeButton("ПОКИНУТЬ", ["DangerButton"], hover: true),
                }});

                col.Children.Add(Label("PrimaryButton + DangerButton side by side (AcceptGameModal context)"));
                col.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children =
                {
                    MakeButton("ПРИНЯТЬ", ["PrimaryButton"]),
                    MakeButton("ОТКЛОНИТЬ", ["DangerButton"]),
                }});

                col.Children.Add(Label("LauncherHeader (play state / stop state)"));
                var launchVm = new MainLauncherViewModel(
                    new StubSteamManager(), new StubSettingsStorage(),
                    new StubGameLaunchSettingsStorage(), new StubCvarSettingsProvider(),
                    new StubVideoSettingsProvider(), new StubBackendApiService(),
                    new StubQueueSocketService(), new StubContentRegistryService(),
                    new StubChatViewModelFactory(), new StubWindowService(), new StubSteamAuthApi(),
                    new StubUiDispatcher(), new StubTriviaRepository());
                var launchVmStop = new MainLauncherViewModel(
                    new StubSteamManager(), new StubSettingsStorage(),
                    new StubGameLaunchSettingsStorage(), new StubCvarSettingsProvider(),
                    new StubVideoSettingsProvider(), new StubBackendApiService(),
                    new StubQueueSocketService(), new StubContentRegistryService(),
                    new StubChatViewModelFactory(), new StubWindowService(), new StubSteamAuthApi(),
                    new StubUiDispatcher(), new StubTriviaRepository());
                launchVmStop.Launch.RunState = GameRunState.OurGameRunning;
                col.Children.Add(new LauncherHeader { Width = 400, Height = 48, DataContext = launchVm });
                col.Children.Add(new LauncherHeader { Width = 400, Height = 48, DataContext = launchVmStop });

                return (new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#131720")),
                    Child = col,
                }, null);
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
