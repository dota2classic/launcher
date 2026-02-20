using System;
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
    private readonly DispatcherTimer _onlineStatsTimer;
    private CancellationTokenSource? _ticketExchangeCts;

    // ── Child ViewModels ──────────────────────────────────────────────────────
    public GameLaunchViewModel Launch { get; }
    public QueueViewModel Queue { get; }
    public RoomViewModel Room { get; }
    public PartyViewModel Party { get; }
    public NotificationAreaViewModel NotificationArea { get; }

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
        ISteamAuthApi steamAuthApi,
        IBackendApiService backendApiService,
        IQueueSocketService queueSocketService)
    {
        _steamManager = steamManager;
        _settingsStorage = settingsStorage;
        _steamAuthApi = steamAuthApi;
        _queueSocketService = queueSocketService;
        _backendApiService = backendApiService;

        var settings = settingsStorage.Get();
        _backendAccessToken = settings.BackendAccessToken;
        _currentUser = steamManager.CurrentUser;
        _avatarImage = SteamAvatarHelper.FromUser(_currentUser);

        // Create child ViewModels
        Launch = new GameLaunchViewModel(settingsStorage, queueSocketService);
        Queue = new QueueViewModel(queueSocketService, backendApiService);
        Room = new RoomViewModel(queueSocketService, backendApiService);
        Party = new PartyViewModel(queueSocketService, backendApiService);
        NotificationArea = new NotificationAreaViewModel(backendApiService, queueSocketService);

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

        // Party invites → floating notifications
        queueSocketService.PartyInviteReceived += msg =>
            Dispatcher.UIThread.Post(() => NotificationArea.AddInvite(msg));
        queueSocketService.PartyInviteExpired += msg =>
            Dispatcher.UIThread.Post(() => NotificationArea.RemoveByInviteId(msg.InviteId));

        // на сайте: updated from socket events
        queueSocketService.OnlineUpdated += msg => Dispatcher.UIThread.Post(() =>
        {
            OnlineSessions = msg.Online?.Length ?? 0;
            OnPropertyChanged(nameof(OnlineStatsText));
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
        _steamManager.OnSteamAuthorizationChanged += ticket => Dispatcher.UIThread.Post(() =>
        {
            _ = ExchangeSteamTicketAsync(ticket);
        });

        if (!string.IsNullOrWhiteSpace(_backendAccessToken))
            _ = EnsureQueueConnectionAsync(_backendAccessToken, CancellationToken.None);

        _ = Party.RefreshPartyAsync();
        _ = Queue.RefreshMatchmakingModesAsync();
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

    public void OpenSettings() => IsSettingsOpen = true;
    public void CloseSettings() => IsSettingsOpen = false;

    public void SetGameDirectory(string? path) => Launch.SetGameDirectory(path);

    // ── Auth flow ─────────────────────────────────────────────────────────────

    private async Task ExchangeSteamTicketAsync(string? ticket)
    {
        _ticketExchangeCts?.Cancel();
        _ticketExchangeCts?.Dispose();
        _ticketExchangeCts = new CancellationTokenSource();
        var ct = _ticketExchangeCts.Token;
        AppLog.Info("Trying to exchange steam ticket");

        if (string.IsNullOrWhiteSpace(ticket))
        {
            AppLog.Info("Steam ticket cleared; backend token reset.");
            BackendAccessToken = null;
            PersistBackendToken(null);
            await EnsureQueueConnectionAsync(null, ct);
            Party.ClearParty();
            return;
        }

        try
        {
            var token = await _steamAuthApi.ExchangeSteamSessionTicketAsync(ticket, ct);
            if (ct.IsCancellationRequested)
                return;

            BackendAccessToken = token;
            PersistBackendToken(token);
            await EnsureQueueConnectionAsync(token, ct);
            await Party.RefreshPartyAsync();
        }
        catch (OperationCanceledException)
        {
            AppLog.Info("Steam ticket exchange canceled.");
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested)
                return;

            AppLog.Error("Steam ticket exchange path failed.", ex);
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
    }
}
