using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Integration;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class MainLauncherViewModel : ViewModelBase, IDisposable
{
    private readonly SteamManager _steamManager;
    private readonly ISettingsStorage _settingsStorage;
    private readonly ISteamAuthApi _steamAuthApi;
    private readonly IQueueSocketService _queueSocketService;
    private readonly IBackendApiService _backendApiService;
    private readonly IHttpImageService _imageService;
    private readonly ICvarSettingsProvider _cvarProvider;
    private readonly IVideoSettingsProvider _videoProvider;
    private readonly IContentRegistryService _registryService;

    /// <summary>
    /// Called when the user applies DLC changes in Settings. Receives the list of
    /// package IDs to remove. The parent ViewModel uses this to re-enter VerifyingGame.
    /// </summary>
    public Action<List<string>>? OnDlcChanged { get; set; }
    private readonly DispatcherTimer _onlineStatsTimer;
    private CancellationTokenSource? _ticketExchangeCts;

    // ── Child ViewModels ──────────────────────────────────────────────────────
    public GameLaunchViewModel Launch { get; }
    public QueueViewModel Queue { get; }
    public RoomViewModel Room { get; }
    public PartyViewModel Party { get; }
    public NotificationAreaViewModel NotificationArea { get; }
    public SettingsViewModel Settings { get; }
    public ChatViewModel Chat { get; }

    // ── Auth / user state ─────────────────────────────────────────────────────
    [ObservableProperty]
    private Models.User? _currentUser;

    [ObservableProperty]
    private Bitmap? _avatarImage;

    [ObservableProperty]
    private string? _backendAccessToken;

    [ObservableProperty]
    private bool _isSettingsOpen;

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
        ISteamAuthApi steamAuthApi,
        IBackendApiService backendApiService,
        IHttpImageService imageService,
        IQueueSocketService queueSocketService,
        IContentRegistryService registryService)
    {
        _steamManager = steamManager;
        _settingsStorage = settingsStorage;
        _cvarProvider = cvarProvider;
        _videoProvider = videoProvider;
        _steamAuthApi = steamAuthApi;
        _queueSocketService = queueSocketService;
        _backendApiService = backendApiService;
        _imageService = imageService;
        _registryService = registryService;

        var settings = settingsStorage.Get();
        _backendAccessToken = settings.BackendAccessToken;
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
        Launch = new GameLaunchViewModel(settingsStorage, launchSettingsStorage, cvarProvider, videoProvider, queueSocketService);
        Queue = new QueueViewModel(queueSocketService, backendApiService);
        Room = new RoomViewModel(queueSocketService, backendApiService);
        Party = new PartyViewModel(queueSocketService, backendApiService);
        NotificationArea = new NotificationAreaViewModel(queueSocketService);
        Settings = new SettingsViewModel(launchSettingsStorage, cvarProvider, settingsStorage, videoProvider, registryService);
        Settings.PushCvar = PushCvarIfGameRunning;
        Settings.OnDlcChanged = removedIds => OnDlcChanged?.Invoke(removedIds);
        Chat = new ChatViewModel(backendApiService, imageService, queueSocketService);
        Chat.GetBackendToken = () => BackendAccessToken;
        _ = Chat.StartAsync();

        // Wire delegates into children that need auth state
        Room.GetCurrentUser = () => CurrentUser;
        Room.GetBackendToken = () => BackendAccessToken;
        Room.GetModeName = mode =>
            Queue.MatchmakingModes.FirstOrDefault(m => m.ModeId == (int)mode)?.Name ?? mode.ToString();
        Party.GetBackendToken = () => BackendAccessToken;

        // Keep queue timer in sync with party queue time
        Party.EnterQueueAtChanged += time => Queue.SetEnterQueueAt(time);

        // Push party restrictions into queue mode list
        Party.PartyMembersChanged += members => Queue.ApplyPartyRestrictions(members);

        // Party invites → floating notifications + sound
        queueSocketService.PartyInviteReceived += msg =>
        {
            Util.SoundPlayer.Play("party_invite.mp3");
            Dispatcher.UIThread.Post(() => NotificationArea.AddInvite(msg));
        };
        queueSocketService.PartyInviteExpired += msg =>
            Dispatcher.UIThread.Post(() => NotificationArea.RemoveByInviteId(msg.InviteId));

        // Match found (initial PLAYER_ROOM_FOUND event only, not subsequent state updates) → sound
        queueSocketService.PlayerRoomFound += _ => Util.SoundPlayer.Play("match_found.mp3");

        // Server assigned (connect now) → sound
        queueSocketService.PlayerGameStateUpdated += msg =>
        {
            if (!string.IsNullOrEmpty(msg?.ServerUrl))
                Util.SoundPlayer.Play("ready_check_no_focus.wav");
        };

        // на сайте: updated from socket events
        queueSocketService.OnlineUpdated += msg => Dispatcher.UIThread.Post(() =>
        {
            OnlineSessions = msg.Online?.Length ?? 0;
            OnPropertyChanged(nameof(OnlineStatsText));
            Queue.OnlineStatsText = OnlineStatsText;
        });

        // в игре: polled from API every 5 seconds
        _onlineStatsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _onlineStatsTimer.Tick += (_, _) => { _ = RefreshInGameCountAsync(); };
        _onlineStatsTimer.Start();
        _ = RefreshInGameCountAsync();

        // Steam events
        _steamManager.OnUserUpdated += u => Dispatcher.UIThread.Post(() =>
        {
            var oldBitmap = AvatarImage;
            CurrentUser = u;
            AvatarImage = SteamAvatarHelper.FromUser(u);
            oldBitmap?.Dispose();
            OnPropertyChanged(nameof(LoggedInAsText));
        });
        _steamManager.OnSteamAuthorizationChanged += token => Dispatcher.UIThread.Post(() =>
        {
            _ = ApplyBackendTokenAsync(token);
        });

        // If the token was already acquired before this VM was created (e.g. the user was on
        // SelectGameView when the SteamManager fired OnSteamAuthorizationChanged), apply it now.
        var currentToken = _steamManager.CurrentAuthTicket;
        if (!string.IsNullOrWhiteSpace(currentToken))
            _ = ApplyBackendTokenAsync(currentToken);
        else if (!string.IsNullOrWhiteSpace(_backendAccessToken))
            _ = EnsureQueueConnectionAsync(_backendAccessToken, CancellationToken.None);

        // When cvar state changes (e.g. config.cfg re-read after game exit), refresh UI
        _cvarProvider.CvarChanged += OnCvarChanged;

        _ = Party.RefreshPartyAsync();
        _ = Queue.RefreshMatchmakingModesAsync();

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

    // ── Settings ──────────────────────────────────────────────────────────────

    public void OpenSettings()
    {
        var gameDir = Launch.GameDirectory;
        if (!string.IsNullOrWhiteSpace(gameDir))
        {
            _videoProvider.LoadFromVideoTxt(gameDir);
            Settings.RefreshFromVideoProvider();
        }
        IsSettingsOpen = true;
    }

    public void CloseSettings() => IsSettingsOpen = false;

    /// <summary>
    /// Called by <see cref="MainWindowViewModel"/> so that a directory change from
    /// the settings panel triggers the manifest scan / download flow.
    /// </summary>
    public Action<string>? OnGameDirectoryChanged { get; set; }

    public void SetGameDirectory(string? path)
    {
        Launch.SetGameDirectory(path);
        if (!string.IsNullOrEmpty(path))
            OnGameDirectoryChanged?.Invoke(path);
    }

    // ── Auth flow ─────────────────────────────────────────────────────────────

    private async Task ApplyBackendTokenAsync(string? token)
    {
        _ticketExchangeCts?.Cancel();
        _ticketExchangeCts?.Dispose();
        _ticketExchangeCts = new CancellationTokenSource();
        var ct = _ticketExchangeCts.Token;

        if (string.IsNullOrWhiteSpace(token))
        {
            AppLog.Info("Backend token cleared.");
            BackendAccessToken = null;
            PersistBackendToken(null);
            await EnsureQueueConnectionAsync(null, ct);
            Party.ClearParty();
            return;
        }

        try
        {
            AppLog.Info("Backend token received from bridge.");
            BackendAccessToken = token;
            PersistBackendToken(token);
            await EnsureQueueConnectionAsync(token, ct);
            await Party.RefreshPartyAsync();
            Chat.RestartSse();
        }
        catch (OperationCanceledException)
        {
            AppLog.Info("Backend token application canceled.");
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested)
                return;

            AppLog.Error("Backend token application failed.", ex);
            BackendAccessToken = null;
            PersistBackendToken(null);
            await EnsureQueueConnectionAsync(null, ct);
            Party.ClearParty();
        }
    }

    private async Task EnsureQueueConnectionAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            await _queueSocketService.DisconnectAsync(cancellationToken);
            return;
        }
        await _queueSocketService.ConnectAsync(token, cancellationToken);
    }

    private async Task RefreshInGameCountAsync()
    {
        var (inGame, _) = await _backendApiService.GetOnlineStatsAsync();
        OnlineInGame = inGame;
        OnPropertyChanged(nameof(OnlineStatsText));
        Queue.OnlineStatsText = OnlineStatsText;
    }

    private void PersistBackendToken(string? token)
    {
        var settings = _settingsStorage.Get();
        settings.BackendAccessToken = token;
        _settingsStorage.Save(settings);
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
        _ticketExchangeCts?.Dispose();
        Launch.Dispose();
        Queue.Dispose();
        Party.Dispose();
        Chat.Dispose();
    }
}
