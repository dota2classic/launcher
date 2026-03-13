using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Integration;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public enum LauncherTab { Play, Live, Profile }

public partial class MainLauncherViewModel : ViewModelBase, IDisposable
{
    private readonly SteamManager _steamManager;
    private readonly ISettingsStorage _settingsStorage;
    private readonly IQueueSocketService _queueSocketService;
    private readonly IBackendApiService _backendApiService;
    private readonly ICvarSettingsProvider _cvarProvider;
    private readonly IVideoSettingsProvider _videoProvider;
    private readonly IContentRegistryService _registryService;
    private readonly AuthCoordinator _authCoordinator;
    private readonly IWindowService _windowService;

    /// <summary>
    /// Called when the user applies DLC changes in Settings. Receives the list of
    /// package IDs to remove. The parent ViewModel uses this to re-enter VerifyingGame.
    /// </summary>
    public Action<List<string>>? OnDlcChanged { get; set; }
    private readonly DispatcherTimer _onlineStatsTimer;
    private readonly SocketSoundCoordinator _soundCoordinator;
    private readonly Action<OnlineUpdateMessage> _onlineUpdatedHandler;
    private readonly Action<User?> _onUserUpdatedHandler;
    private readonly Action<string?> _tokenAppliedHandler;

    // ── Child ViewModels ──────────────────────────────────────────────────────
    public GameLaunchViewModel Launch { get; }
    public QueueViewModel Queue { get; }
    public RoomViewModel Room { get; }
    public PartyViewModel Party { get; }
    public NotificationAreaViewModel NotificationArea { get; }
    public SettingsViewModel Settings { get; }
    public ChatViewModel Chat { get; }
    public ProfileViewModel Profile { get; }

    // ── Auth / user state ─────────────────────────────────────────────────────
    [ObservableProperty]
    private Models.User? _currentUser;

    [ObservableProperty]
    private Bitmap? _avatarImage;

    [ObservableProperty]
    private LauncherTab _activeTab = LauncherTab.Play;

    public bool IsPlayTabActive => ActiveTab == LauncherTab.Play;
    public bool IsLiveTabActive => ActiveTab == LauncherTab.Live;
    public bool IsProfileTabActive => ActiveTab == LauncherTab.Profile;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private bool _isIntroOpen;

    [ObservableProperty]
    private int _introStep = 1;

    public int IntroStepCount => 4;

    [ObservableProperty]
    private int _onlineInGame;

    [ObservableProperty]
    private int _onlineSessions;

    public string OnlineStatsText => $"{OnlineInGame} в игре, {OnlineSessions} на сайте";

    public string LoggedInAsText => CurrentUser != null
        ? "Logged in as: " + CurrentUser.PersonaName
        : "Steam offline or not logged in.";

    public MainLauncherViewModel(
        SteamManager steamManager,
        ISettingsStorage settingsStorage,
        IGameLaunchSettingsStorage launchSettingsStorage,
        ICvarSettingsProvider cvarProvider,
        IVideoSettingsProvider videoProvider,
        IBackendApiService backendApiService,
        IQueueSocketService queueSocketService,
        IContentRegistryService registryService,
        IChatViewModelFactory chatViewModelFactory,
        IWindowService windowService)
    {
        _steamManager = steamManager;
        _settingsStorage = settingsStorage;
        _cvarProvider = cvarProvider;
        _videoProvider = videoProvider;
        _queueSocketService = queueSocketService;
        _backendApiService = backendApiService;
        _registryService = registryService;
        _windowService = windowService;

        var settings = settingsStorage.Get();
        _isIntroOpen = !settings.IntroShown;

        _authCoordinator = new AuthCoordinator(steamManager, backendApiService, queueSocketService, settingsStorage);
        _currentUser = steamManager.CurrentUser;
        _avatarImage = SteamAvatarHelper.FromUser(_currentUser);

        // Seed settings from game config files on startup
        var gameDir = settings.GameDirectory;
        AppLog.Info($"Saved game folder: {(string.IsNullOrWhiteSpace(gameDir) ? "(not set)" : gameDir)}");
        if (!string.IsNullOrWhiteSpace(gameDir))
        {
            cvarProvider.LoadFromConfigCfg(gameDir);
            videoProvider.LoadFromVideoTxt(gameDir);
        }

        // Create child ViewModels
        Launch = new GameLaunchViewModel(settingsStorage, launchSettingsStorage, cvarProvider, videoProvider, queueSocketService, backendApiService);
        Queue = new QueueViewModel(queueSocketService, backendApiService);
        Room = new RoomViewModel(queueSocketService, backendApiService);
        Party = new PartyViewModel(queueSocketService, backendApiService);
        NotificationArea = new NotificationAreaViewModel(queueSocketService);
        Queue.ShowNoModesSelectedToast = () =>
            NotificationArea.AddToast("Выберите хотя бы один режим игры для поиска");
        Settings = new SettingsViewModel(launchSettingsStorage, cvarProvider, settingsStorage, videoProvider, registryService);
        Settings.PushCvar = PushCvarIfGameRunning;
        Settings.OnDlcChanged = removedIds => OnDlcChanged?.Invoke(removedIds);
        Chat = chatViewModelFactory.Create("17aa3530-d152-462e-a032-909ae69019ed");
        Chat.OpenPlayerProfile = OpenPlayerProfile;
        FireAndForget(Chat.StartAsync(), "Chat.StartAsync");
        Profile = new ProfileViewModel(backendApiService);

        _soundCoordinator = new SocketSoundCoordinator(queueSocketService, NotificationArea, windowService);

        // Wire delegates into children that need auth state
        Room.GetCurrentUser = () => CurrentUser;
        Room.GetModeName = mode =>
            Queue.MatchmakingModes.FirstOrDefault(m => m.ModeId == (int)mode)?.Name ?? mode.ToString();

        // Keep queue timer in sync with party queue time
        Party.EnterQueueAtChanged += time => Queue.SetEnterQueueAt(time);

        // Push party restrictions into queue mode list
        Party.PartyMembersChanged += members => Queue.ApplyPartyRestrictions(members);

        // на сайте: updated from socket events
        _onlineUpdatedHandler = msg => Dispatcher.UIThread.Post(() =>
        {
            OnlineSessions = msg.Online?.Length ?? 0;
            OnPropertyChanged(nameof(OnlineStatsText));
            Queue.OnlineStatsText = OnlineStatsText;
        });
        queueSocketService.OnlineUpdated += _onlineUpdatedHandler;

        // в игре: polled from API every 5 seconds
        _onlineStatsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _onlineStatsTimer.Tick += (_, _) => FireAndForget(RefreshInGameCountAsync(), "RefreshInGameCountAsync (timer)");
        _onlineStatsTimer.Start();
        FireAndForget(RefreshInGameCountAsync(), "RefreshInGameCountAsync (startup)");

        // Steam user profile events
        _onUserUpdatedHandler = u => Dispatcher.UIThread.Post(() =>
        {
            var oldBitmap = AvatarImage;
            CurrentUser = u;
            AvatarImage = SteamAvatarHelper.FromUser(u);
            oldBitmap?.Dispose();
            OnPropertyChanged(nameof(LoggedInAsText));
        });
        _steamManager.OnUserUpdated += _onUserUpdatedHandler;

        _tokenAppliedHandler = token =>
        {
            if (token != null)
            {
                FireAndForget(Party.RefreshPartyAsync(), "RefreshPartyAsync (TokenApplied)");
                Chat.RestartSse();
            }
            else
            {
                Party.ClearParty();
            }
        };
        _authCoordinator.TokenApplied += _tokenAppliedHandler;
        _authCoordinator.Start(settings.BackendAccessToken);

        // When cvar state changes (e.g. config.cfg re-read after game exit), refresh UI
        _cvarProvider.CvarChanged += OnCvarChanged;

        FireAndForget(Party.RefreshPartyAsync(), "Party.RefreshPartyAsync (startup)");
        FireAndForget(Queue.RefreshMatchmakingModesAsync(), "Queue.RefreshMatchmakingModesAsync (startup)");

    }

    private static async void FireAndForget(Task task, string context)
    {
        try { await task; }
        catch (Exception ex) { AppLog.Error($"FireAndForget({context})", ex); }
    }

    private void OnCvarChanged()
    {
        Dispatcher.UIThread.Post(() => Settings.RefreshFromCvarProvider());
    }

    private void PushCvarIfGameRunning(string cvar, string value)
    {
        if (Launch.RunState == GameRunState.OurGameRunning)
            DotaConsoleConnector.SendCommand($"{cvar} {value}");
    }

    // ── Search / connect ──────────────────────────────────────────────────────

    public async Task ToggleSearchAsync()
    {
        if (Launch.HasServerUrl)
        {
            Launch.ConnectToGame();
            return;
        }
        await Queue.ToggleSearchAsync();
    }

    partial void OnActiveTabChanged(LauncherTab value)
    {
        OnPropertyChanged(nameof(IsPlayTabActive));
        OnPropertyChanged(nameof(IsLiveTabActive));
        OnPropertyChanged(nameof(IsProfileTabActive));
    }

    // ── Tab navigation ────────────────────────────────────────────────────────

    public void NavigateTo(LauncherTab tab) => ActiveTab = tab;

    public void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
        if (IsSettingsOpen)
        {
            var gameDir = Launch.GameDirectory;
            if (!string.IsNullOrWhiteSpace(gameDir))
            {
                _videoProvider.LoadFromVideoTxt(gameDir);
                Settings.RefreshFromVideoProvider();
            }
            FireAndForget(Settings.LoadDlcPackagesAsync(), "Settings.LoadDlcPackagesAsync");
        }
    }

    public void OpenSettings() => IsSettingsOpen = true;
    public void CloseSettings() => IsSettingsOpen = false;

    [RelayCommand]
    private void CloseSettingsRelay() => CloseSettings();
    public void CloseProfile() => NavigateTo(LauncherTab.Play);

    public void OpenProfile()
    {
        if (CurrentUser == null) return;
        var steam32 = (CurrentUser.SteamId - 76561197960265728UL).ToString();
        OpenPlayerProfile(steam32);
    }

    /// <summary>Navigates to the profile tab and loads the specified player. Receives steam32 ID.</summary>
    public void OpenPlayerProfile(string steam32Id)
    {
        FireAndForget(Profile.LoadAsync(steam32Id), "Profile.LoadAsync");
        ActiveTab = LauncherTab.Profile;
    }

    // ── Intro ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void NextIntroStep()
    {
        if (IntroStep < IntroStepCount)
        {
            IntroStep++;
        }
        else
        {
            CloseIntro();
        }
    }

    [RelayCommand]
    private void CloseIntro()
    {
        IsIntroOpen = false;
        var settings = _settingsStorage.Get();
        settings.IntroShown = true;
        _settingsStorage.Save(settings);
    }

    /// <summary>
    /// Called by <see cref="MainWindowViewModel"/> so that a directory change from
    /// the settings panel triggers the manifest scan / download flow.
    /// </summary>
    public Action<string>? OnGameDirectoryChanged { get; set; }

    /// <summary>
    /// Called when the user requests to change the game directory from settings.
    /// The caller should navigate to the game selection screen.
    /// </summary>
    public Action? RequestGameDirectoryChange { get; set; }

    public void SetGameDirectory(string? path)
    {
        Launch.SetGameDirectory(path);
        if (!string.IsNullOrEmpty(path))
            OnGameDirectoryChanged?.Invoke(path);
    }

    private async Task RefreshInGameCountAsync()
    {
        var (inGame, _) = await _backendApiService.GetOnlineStatsAsync();
        OnlineInGame = inGame;
        OnPropertyChanged(nameof(OnlineStatsText));
        Queue.OnlineStatsText = OnlineStatsText;
    }

    // Forwarded for code-behind convenience
    public void LaunchGame() => Launch.LaunchGame();
    public bool IsGameDirectorySet => Launch.IsGameDirectorySet;
    public void OpenInviteModal() => Party.OpenInviteModal();
    public void CloseInviteModal() => Party.CloseInviteModal();
    public async Task InvitePlayerAsync(string steamId) => await Party.InvitePlayerAsync(steamId);

    public void Dispose()
    {
        _onlineStatsTimer.Stop();
        _queueSocketService.OnlineUpdated -= _onlineUpdatedHandler;
        _steamManager.OnUserUpdated -= _onUserUpdatedHandler;
        _authCoordinator.TokenApplied -= _tokenAppliedHandler;
        _cvarProvider.CvarChanged -= OnCvarChanged;
        _soundCoordinator.Dispose();
        _authCoordinator.Dispose();
        Launch.Dispose();
        Queue.Dispose();
        Party.Dispose();
        Chat.Dispose();
    }
}
