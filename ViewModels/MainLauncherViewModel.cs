using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Api;
using d2c_launcher.Integration;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class MainLauncherViewModel : ViewModelBase
{
    private readonly SteamManager _steamManager;
    private readonly ISettingsStorage _settingsStorage;
    private readonly ISteamAuthApi _steamAuthApi;
    private readonly IBackendApiService _backendApiService;
    private readonly IQueueSocketService _queueSocketService;
    private readonly DispatcherTimer _runStateTimer;
    private readonly DispatcherTimer _partyRefreshTimer;
    private readonly DispatcherTimer _queueTimer;
    private CancellationTokenSource? _ticketExchangeCts;
    private CancellationTokenSource? _inviteSearchCts;
    private int _partyRefreshRunning;
    private HashSet<string> _onlineUsers = new(StringComparer.Ordinal);
    private DateTimeOffset? _enterQueueAt;
    private int _queuedModeCount;

    [ObservableProperty]
    private User? _currentUser;

    [ObservableProperty]
    private string? _gameDirectory;

    [ObservableProperty]
    private Bitmap? _avatarImage;

    [ObservableProperty]
    private GameRunState _runState;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private string? _backendAccessToken;

    [ObservableProperty]
    private ObservableCollection<PartyMemberView> _partyMembers = new();

    [ObservableProperty]
    private ObservableCollection<MatchmakingModeView> _matchmakingModes = new();

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _searchingModesText = "Не в поиске";

    [ObservableProperty]
    private bool _isInviteModalOpen;

    [ObservableProperty]
    private string _inviteSearchText = "";

    [ObservableProperty]
    private ObservableCollection<InviteCandidateView> _inviteCandidates = new();

    [ObservableProperty]
    private string _queueButtonMainText = "Ð˜Ð“Ð ÐÐ¢Ð¬";

    [ObservableProperty]
    private string _queueButtonModeCountText = "";

    [ObservableProperty]
    private string _queueButtonTimeText = "";

    [ObservableProperty]
    private bool _isAcceptGameModalOpen;

    [ObservableProperty]
    private bool _isServerSearchingModalOpen;

    [ObservableProperty]
    private bool _hasMyPlayerAccepted;

    /// <summary>True when the local player has already sent any response (Ready or Decline) — hides the Accept/Decline buttons.</summary>
    [ObservableProperty]
    private bool _hasMyPlayerResponded;

    [ObservableProperty]
    private string? _serverUrl;

    public bool HasServerUrl => !string.IsNullOrEmpty(ServerUrl);

    partial void OnServerUrlChanged(string? value)
    {
        OnPropertyChanged(nameof(HasServerUrl));
        UpdateQueueButtonState();
    }

    [ObservableProperty]
    private ObservableCollection<RoomPlayerView> _roomPlayers = new();

    [ObservableProperty]
    private string? _currentRoomId;

    [ObservableProperty]
    private MatchmakingMode? _roomMode;

    public string RoomModeText => RoomMode.HasValue
        ? MatchmakingModes.FirstOrDefault(m => m.ModeId == (int)RoomMode.Value)?.Name ?? RoomMode.Value.ToString()
        : "";

    partial void OnRoomModeChanged(MatchmakingMode? value) => OnPropertyChanged(nameof(RoomModeText));

    /// <summary>Blue when game ready, green when searching, dark gray when idle.</summary>
    public IBrush QueueButtonBackground => HasServerUrl
        ? new SolidColorBrush(Color.Parse("#1A5276"))
        : IsSearching ? new SolidColorBrush(Color.Parse("#1F8B4C")) : new SolidColorBrush(Color.Parse("#3A3A3A"));

    /// <summary>Lighter version for hover state.</summary>
    public IBrush QueueButtonHoverBackground => HasServerUrl
        ? new SolidColorBrush(Color.Parse("#2E86C1"))
        : IsSearching ? new SolidColorBrush(Color.Parse("#2F9B5C")) : new SolidColorBrush(Color.Parse("#4A4A4A"));

    public IRelayCommand CloseInviteModalCommand { get; }
    public IRelayCommand AcceptGameCommand { get; }
    public IRelayCommand DeclineGameCommand { get; }

    public string? SteamAuthTicket { get; private set; }

    /// <summary>Display text for the current user, or a message when not logged in.</summary>
    public string LoggedInAsText => CurrentUser != null
        ? "Logged in as: " + CurrentUser.PersonaName
        : "Steam offline or not logged in.";

    public bool IsGameDirectorySet => !string.IsNullOrWhiteSpace(GameDirectory);

    public string LaunchButtonText => RunState switch
    {
        _ when !IsGameDirectorySet => "Select game",
        GameRunState.OurGameRunning => "Running",
        GameRunState.OtherDotaRunning => "Another dota running",
        _ => "Play"
    };

    public bool IsLaunchEnabled => !IsGameDirectorySet || RunState == GameRunState.None;
    public bool CanInviteToParty => PartyMembers.Count < 5;

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
        _backendApiService = backendApiService;
        _queueSocketService = queueSocketService;
        var settings = settingsStorage.Get();
        _gameDirectory = settings.GameDirectory;
        _backendAccessToken = settings.BackendAccessToken;
        _currentUser = steamManager.CurrentUser;
        SteamAuthTicket = null;
        _avatarImage = SteamAvatarHelper.FromUser(_currentUser);
        _runState = GameRunState.None;

        _runStateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _runStateTimer.Tick += (_, _) => RefreshRunState();
        _runStateTimer.Start();
        RefreshRunState();

        _partyRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _partyRefreshTimer.Tick += (_, _) => _ = RefreshPartyAsync();
        _partyRefreshTimer.Start();

        _queueTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _queueTimer.Tick += (_, _) => UpdateQueueButtonState();
        _queueTimer.Start();

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
            SteamAuthTicket = ticket;
            _ = ExchangeSteamTicketAsync(ticket);
        });

        _queueSocketService.PartyUpdated += party => Dispatcher.UIThread.Post(() => _ = RefreshPartyAsync());
        _queueSocketService.QueueStateUpdated += msg => Dispatcher.UIThread.Post(() => UpdateQueueCounts(msg));
        _queueSocketService.PlayerQueueStateUpdated += msg => Dispatcher.UIThread.Post(() => UpdatePlayerQueueState(msg));
        _queueSocketService.PlayerRoomStateUpdated += msg => Dispatcher.UIThread.Post(() => _ = UpdatePlayerRoomStateAsync(msg));
        _queueSocketService.PlayerGameStateUpdated += msg => Dispatcher.UIThread.Post(() => UpdatePlayerGameState(msg));
        _queueSocketService.ServerSearchingUpdated += msg => Dispatcher.UIThread.Post(() => UpdateServerSearching(msg));
        _queueSocketService.OnlineUpdated += msg => Dispatcher.UIThread.Post(() => UpdateOnlineUsers(msg));

        CloseInviteModalCommand = new RelayCommand(CloseInviteModal);
        AcceptGameCommand = new RelayCommand(AcceptGame);
        DeclineGameCommand = new RelayCommand(DeclineGame);

        if (!string.IsNullOrWhiteSpace(_backendAccessToken))
            _ = EnsureQueueConnectionAsync(_backendAccessToken, CancellationToken.None);

        _ = RefreshPartyAsync();
        _ = RefreshMatchmakingModesAsync();
        UpdateQueueButtonState();
    }

    private async Task ExchangeSteamTicketAsync(string? ticket)
    {
        _ticketExchangeCts?.Cancel();
        _ticketExchangeCts?.Dispose();
        _ticketExchangeCts = new CancellationTokenSource();
        var ct = _ticketExchangeCts.Token;
        AppLog.Info($"Trying to exchange steam ticket");

        if (string.IsNullOrWhiteSpace(ticket))
        {
            AppLog.Info("Steam ticket cleared; backend token reset.");
            BackendAccessToken = null;
            PersistBackendToken(null);
            await EnsureQueueConnectionAsync(null, ct);
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
            await RefreshPartyAsync();
        }
        catch (OperationCanceledException)
        {
            // Ignore rapid user/ticket changes.
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
            ClearParty();
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

    private void PersistBackendToken(string? token)
    {
        var settings = _settingsStorage.Get();
        settings.BackendAccessToken = token;
        _settingsStorage.Save(settings);
    }

    public async Task RefreshPartyAsync()
    {
        if (Interlocked.Exchange(ref _partyRefreshRunning, 1) != 0)
            return;

        try
        {
            if (string.IsNullOrWhiteSpace(BackendAccessToken))
            {
                AppLog.Info("Party refresh skipped: no backend token.");
                ClearParty();
                return;
            }

            var partySnapshot = await _backendApiService.GetMyPartySnapshotAsync(BackendAccessToken);
            DisposePartyAvatars();
            PartyMembers.Clear();
            foreach (var m in partySnapshot.Members)
                PartyMembers.Add(m);

            _enterQueueAt = partySnapshot.EnterQueueAt;
            UpdateQueueButtonState();
            OnPropertyChanged(nameof(CanInviteToParty));
        }
        catch (Exception ex)
        {
            AppLog.Error("Party refresh failed.", ex);
            ClearParty();
        }
        finally
        {
            Interlocked.Exchange(ref _partyRefreshRunning, 0);
        }
    }

    private void ClearParty()
    {
        DisposePartyAvatars();
        PartyMembers.Clear();
        _enterQueueAt = null;
        UpdateQueueButtonState();
        OnPropertyChanged(nameof(CanInviteToParty));
    }

    private void UpdateQueueCounts(QueueStateMessage msg)
    {
        var id = (int)msg.Mode;
        foreach (var mode in MatchmakingModes)
        {
            if (mode.ModeId == id)
            {
                mode.InQueue = msg.InQueue;
                break;
            }
        }
    }

    private void UpdatePlayerQueueState(PlayerQueueStateMessage msg)
    {
        if (msg.InQueue)
        {
            _queuedModeCount = msg.Modes?.Length ?? 0;
            var names = MatchmakingModes
                .Where(m => msg.Modes.Any(x => (int)x == m.ModeId))
                .Select(m => m.Name)
                .ToArray();
            SearchingModesText = names.Length > 0
                ? $"В поиске: {string.Join(", ", names)}"
                : "В поиске";
        }
        else
        {
            _queuedModeCount = 0;
            SearchingModesText = "Не в поиске";
        }

        IsSearching = msg.InQueue;
        UpdateQueueButtonState();
    }

    private void UpdateQueueButtonState()
    {
        if (HasServerUrl)
        {
            QueueButtonMainText = "ПОДКЛЮЧИТЬСЯ";
            QueueButtonModeCountText = "";
            QueueButtonTimeText = "";
        }
        else if (IsSearching)
        {
            QueueButtonMainText = "ОТМЕНИТЬ ПОИСК";
            QueueButtonModeCountText = _queuedModeCount > 0 ? FormatModeCount(_queuedModeCount) : "";
            var elapsed = _enterQueueAt.HasValue
                ? DateTimeOffset.UtcNow.Subtract(_enterQueueAt.Value.UtcDateTime)
                : TimeSpan.Zero;
            QueueButtonTimeText = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
        }
        else
        {
            QueueButtonMainText = "ИГРАТЬ";
            QueueButtonModeCountText = "";
            QueueButtonTimeText = "";
        }
        OnPropertyChanged(nameof(QueueButtonBackground));
        OnPropertyChanged(nameof(QueueButtonHoverBackground));
    }

    private static string FormatModeCount(int n)
    {
        if (n == 1) return "1 РЕЖИМ";
        if (n >= 2 && n <= 4) return $"{n} РЕЖИМА";
        return $"{n} РЕЖИМОВ";
    }

    public async Task RefreshMatchmakingModesAsync()
    {
        try
        {
            var modes = await _backendApiService.GetEnabledMatchmakingModesAsync();
            var next = new ObservableCollection<MatchmakingModeView>();

            foreach (var mode in modes)
            {
                var existing = MatchmakingModes.FirstOrDefault(m => m.ModeId == mode.ModeId);
                var view = new MatchmakingModeView(mode.ModeId, mode.Name, existing?.IsSelected ?? false)
                {
                    InQueue = existing?.InQueue ?? 0
                };
                next.Add(view);
            }

            MatchmakingModes = next;
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to load matchmaking modes.", ex);
        }
    }

    public async Task ToggleSearchAsync()
    {
        if (HasServerUrl)
        {
            ConnectToGame();
            return;
        }

        if (IsSearching)
        {
            await _queueSocketService.LeaveAllQueuesAsync();
            return;
        }

        var selected = MatchmakingModes.Where(m => m.IsSelected)
            .Select(m => (MatchmakingMode)m.ModeId)
            .ToArray();
        if (selected.Length == 0)
            return;

        await _queueSocketService.EnterQueueAsync(selected);
    }

    private void ConnectToGame() => _ = ConnectToGameAsync();

    private async Task ConnectToGameAsync()
    {
        var url = ServerUrl;
        if (string.IsNullOrEmpty(url))
            return;

        AppLog.Info($"ConnectToGame: serverUrl={url}");

        // If another Dota (not ours) is running, kill it first
        if (RunState == GameRunState.OtherDotaRunning)
        {
            AppLog.Info("ConnectToGame: killing foreign Dota processes");
            KillAllDotaProcesses();
            await Task.Delay(1500);
            RefreshRunState();
        }

        // Launch our Dota if it's not already up
        if (RunState != GameRunState.OurGameRunning)
        {
            if (string.IsNullOrWhiteSpace(GameDirectory))
            {
                AppLog.Info("ConnectToGame: no game directory set, cannot launch");
                return;
            }

            AppLog.Info("ConnectToGame: launching our Dota");
            LaunchGame();
        }

        // Poll for the DOTA 2 window (up to 90 s)
        AppLog.Info("ConnectToGame: waiting for DOTA 2 window...");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(1500);
            if (DotaConsoleConnector.IsWindowOpen())
                break;
        }

        if (!DotaConsoleConnector.IsWindowOpen())
        {
            AppLog.Info("ConnectToGame: timed out waiting for DOTA 2 window");
            return;
        }

        // Give the console subsystem a moment to finish initializing
        await Task.Delay(3000);

        AppLog.Info($"ConnectToGame: sending 'connect {url}'");
        DotaConsoleConnector.SendCommand($"connect {url}");
    }

    private static void KillAllDotaProcesses()
    {
        var processes = Process.GetProcessesByName("dota");
        try
        {
            foreach (var p in processes)
            {
                try { p.Kill(entireProcessTree: true); } catch { }
            }
        }
        finally
        {
            foreach (var p in processes)
                p.Dispose();
        }
    }

    private void UpdateServerSearching(PlayerServerSearchingMessage msg)
    {
        AppLog.Info($"UpdateServerSearching: searching={msg.Searching}");
        IsServerSearchingModalOpen = msg.Searching;
        // Keep the room modal visible while searching
        if (msg.Searching)
            IsAcceptGameModalOpen = true;
    }

    private void UpdatePlayerGameState(PlayerGameStateMessage? msg)
    {
        if (msg == null)
        {
            AppLog.Info("UpdatePlayerGameState: cleared");
            ServerUrl = null;
            return;
        }

        AppLog.Info($"UpdatePlayerGameState: serverUrl={msg.ServerUrl}");
        ServerUrl = msg.ServerUrl;
        // Close ready-check and server-searching modals now that the game is starting
        IsAcceptGameModalOpen = false;
        IsServerSearchingModalOpen = false;
    }

    public void OpenInviteModal()
    {
        IsInviteModalOpen = true;
        _ = LoadInitialInviteCandidatesAsync();
    }

    public void CloseInviteModal()
    {
        IsInviteModalOpen = false;
        InviteSearchText = "";
    }

    partial void OnInviteSearchTextChanged(string value)
    {
        AppLog.Info($"Invite search text changed: '{value}'");
        _ = SearchInviteCandidatesAsync(value);
    }

    private async Task SearchInviteCandidatesAsync(string query)
    {
        _inviteSearchCts?.Cancel();
        _inviteSearchCts?.Dispose();
        _inviteSearchCts = new CancellationTokenSource();
        var ct = _inviteSearchCts.Token;

        try
        {
            AppLog.Info($"Invite search started: '{query}'");
            await Task.Delay(300, ct);
            if (ct.IsCancellationRequested)
                return;

            if (string.IsNullOrWhiteSpace(query) || query.Length < 1)
            {
                AppLog.Info("Invite search cleared (empty query).");
                InviteCandidates = new ObservableCollection<InviteCandidateView>();
                return;
            }

            var results = await _backendApiService.SearchPlayersAsync(query, 25, ct);
            if (ct.IsCancellationRequested)
                return;

            AppLog.Info($"Invite search results: {results.Count}");
            Dispatcher.UIThread.Post(() =>
            {
                InviteCandidates = new ObservableCollection<InviteCandidateView>(results);
                foreach (var candidate in InviteCandidates)
                    candidate.IsOnline = _onlineUsers.Contains(candidate.SteamId);
            });
        }
        catch (OperationCanceledException)
        {
            // Ignore rapid search changes.
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to search players.", ex);
        }
    }

    private async Task LoadInitialInviteCandidatesAsync()
    {
        if (!string.IsNullOrWhiteSpace(InviteSearchText))
            return;

        try
        {
            AppLog.Info("Loading initial invite candidates.");
            var results = await _backendApiService.SearchPlayersAsync("a", 10);
            Dispatcher.UIThread.Post(() =>
            {
                InviteCandidates = new ObservableCollection<InviteCandidateView>(results);
            });
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to load initial invite candidates.", ex);
        }
    }

    private void UpdateOnlineUsers(OnlineUpdateMessage msg)
    {
        _onlineUsers = msg.Online?.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in InviteCandidates)
            candidate.IsOnline = _onlineUsers.Contains(candidate.SteamId);
    }

    public async Task InvitePlayerAsync(string steamId)
    {
        if (string.IsNullOrWhiteSpace(steamId))
            return;

        try
        {
            await _queueSocketService.InviteToPartyAsync(steamId);
            AppLog.Info($"Invite sent for steamId: {steamId}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to invite player to party.", ex);
        }
    }

    private async Task UpdatePlayerRoomStateAsync(PlayerRoomStateMessage? msg)
    {
        if (msg == null)
        {
            AppLog.Info("UpdatePlayerRoomStateAsync: msg=null (room cleared)");
            // Room cancelled or cleared
            IsAcceptGameModalOpen = false;
            HasMyPlayerAccepted = false;
            HasMyPlayerResponded = false;
            CurrentRoomId = null;
            RoomMode = null;
            DisposeRoomAvatars();
            RoomPlayers.Clear();
            return;
        }

        AppLog.Info($"UpdatePlayerRoomStateAsync: roomId={msg.RoomId}, mode={msg.Mode}, entries={msg.Entries?.Length ?? 0}");
        foreach (var e in msg.Entries ?? Array.Empty<PlayerRoomEntry>())
            AppLog.Info($"  Entry: steamId={e.SteamId}, state={e.State}");

        CurrentRoomId = msg.RoomId;
        RoomMode = msg.Mode;

        // Update existing players or add new ones
        var steamIds = msg.Entries.Select(e => e.SteamId).ToHashSet(StringComparer.Ordinal);
        
        // Remove players no longer in room
        for (int i = RoomPlayers.Count - 1; i >= 0; i--)
        {
            if (!steamIds.Contains(RoomPlayers[i].SteamId))
            {
                RoomPlayers[i].AvatarImage?.Dispose();
                RoomPlayers.RemoveAt(i);
            }
        }

        // Update or add players
        foreach (var entry in msg.Entries)
        {
            var existing = RoomPlayers.FirstOrDefault(p => p.SteamId == entry.SteamId);
            if (existing != null)
            {
                // Update state
                existing.State = entry.State;
            }
            else
            {
                // Add new player - fetch user info
                try
                {
                    var user = await FetchUserInfoAsync(entry.SteamId);
                    // Room may have been cleared while we were awaiting — bail out
                    if (CurrentRoomId != msg.RoomId)
                    {
                        AppLog.Info($"UpdatePlayerRoomStateAsync: room changed during fetch, aborting (expected={msg.RoomId}, current={CurrentRoomId})");
                        user?.AvatarImage?.Dispose();
                        return;
                    }
                    var roomPlayer = new RoomPlayerView(
                        entry.SteamId,
                        user?.Name ?? entry.SteamId,
                        user?.AvatarImage,
                        entry.State
                    );
                    RoomPlayers.Add(roomPlayer);
                }
                catch (Exception ex)
                {
                    AppLog.Error($"Failed to fetch user info for {entry.SteamId}", ex);
                    if (CurrentRoomId != msg.RoomId)
                        return;
                    var roomPlayer = new RoomPlayerView(
                        entry.SteamId,
                        entry.SteamId,
                        null,
                        entry.State
                    );
                    RoomPlayers.Add(roomPlayer);
                }
            }
        }

        // Track whether the local player has already responded (any non-Pending state)
        // Backend uses SteamID3 (32-bit account ID); local SteamID64 → lower 32 bits gives account ID
        var myId = CurrentUser != null
            ? ((uint)(CurrentUser.SteamId & 0xFFFFFFFF)).ToString()
            : null;
        var myEntry = myId != null ? msg.Entries.FirstOrDefault(e => e.SteamId == myId) : null;
        HasMyPlayerAccepted = myEntry?.State == ReadyState.Ready;
        HasMyPlayerResponded = myEntry != null && myEntry.State != ReadyState.Pending;

        // Open modal if not already open
        if (!IsAcceptGameModalOpen)
        {
            IsAcceptGameModalOpen = true;
        }
    }

    private async Task<(string? Name, Bitmap? AvatarImage)?> FetchUserInfoAsync(string steamId)
    {
        if (string.IsNullOrWhiteSpace(BackendAccessToken))
            return null;

        try
        {
            using var httpClient = new System.Net.Http.HttpClient
            {
                BaseAddress = new Uri("https://api.dotaclassic.ru/"),
                Timeout = TimeSpan.FromSeconds(5)
            };
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", BackendAccessToken);

            var api = new DotaclassicApiClient(httpClient);
            var user = await api.PlayerController_userAsync(steamId);
            
            if (user == null)
                return null;

            Bitmap? avatar = null;
            var avatarUrl = user.AvatarSmall ?? user.Avatar;
            if (!string.IsNullOrWhiteSpace(avatarUrl))
            {
                try
                {
                    var uri = new Uri(avatarUrl, UriKind.RelativeOrAbsolute);
                    if (!uri.IsAbsoluteUri)
                        uri = new Uri(new Uri("https://api.dotaclassic.ru/"), uri);

                    using var response = await httpClient.GetAsync(uri);
                    if (response.IsSuccessStatusCode)
                    {
                        await using var stream = await response.Content.ReadAsStreamAsync();
                        avatar = new Bitmap(stream);
                    }
                }
                catch
                {
                    // Ignore avatar load failures
                }
            }

            return (user.Name, avatar);
        }
        catch
        {
            return null;
        }
    }

    private void AcceptGame()
    {
        if (string.IsNullOrWhiteSpace(CurrentRoomId))
            return;

        _ = AcceptGameAsync();
    }

    private async Task AcceptGameAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentRoomId))
            return;

        try
        {
            await _queueSocketService.SetReadyCheckAsync(CurrentRoomId, true);
            AppLog.Info($"Accepted game for room: {CurrentRoomId}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to accept game.", ex);
        }
    }

    private void DeclineGame()
    {
        if (string.IsNullOrWhiteSpace(CurrentRoomId))
            return;

        _ = DeclineGameAsync();
    }

    private async Task DeclineGameAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentRoomId))
            return;

        try
        {
            await _queueSocketService.SetReadyCheckAsync(CurrentRoomId, false);
            AppLog.Info($"Declined game for room: {CurrentRoomId}");
            
            // Close modal after declining
            IsAcceptGameModalOpen = false;
            HasMyPlayerAccepted = false;
            HasMyPlayerResponded = false;
            CurrentRoomId = null;
            RoomMode = null;
            DisposeRoomAvatars();
            RoomPlayers.Clear();
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to decline game.", ex);
        }
    }

    private void DisposeRoomAvatars()
    {
        foreach (var player in RoomPlayers)
            player.AvatarImage?.Dispose();
    }

    private void DisposePartyAvatars()
    {
        foreach (var member in PartyMembers)
            member.AvatarImage?.Dispose();
    }

    public void RefreshRunState()
    {
        if (!IsGameDirectorySet)
        {
            if (RunState != GameRunState.None)
            {
                RunState = GameRunState.None;
                NotifyLaunchProps();
            }
            return;
        }

        var ourExePath = GetOurDotaExePath();
        if (string.IsNullOrEmpty(ourExePath))
        {
            if (RunState != GameRunState.None)
            {
                RunState = GameRunState.None;
                NotifyLaunchProps();
            }
            return;
        }

        var processes = Process.GetProcessesByName("dota");
        try
        {
            var ourRunning = false;
            var otherRunning = false;
            foreach (var p in processes)
            {
                try
                {
                    var path = ProcessPathHelper.TryGetExecutablePath(p);
                    if (string.IsNullOrEmpty(path))
                        otherRunning = true;
                    else if (string.Equals(path, ourExePath, StringComparison.OrdinalIgnoreCase))
                        ourRunning = true;
                    else
                        otherRunning = true;
                }
                catch
                {
                    otherRunning = true;
                }
            }

            var newState = ourRunning ? GameRunState.OurGameRunning : (otherRunning ? GameRunState.OtherDotaRunning : GameRunState.None);
            if (RunState != newState)
            {
                RunState = newState;
                NotifyLaunchProps();
            }
        }
        finally
        {
            foreach (var p in processes)
                p.Dispose();
        }
    }

    private string? GetOurDotaExePath()
    {
        if (string.IsNullOrWhiteSpace(GameDirectory))
            return null;
        var exe = Path.Combine(GameDirectory, "dota.exe");
        try
        {
            return Path.GetFullPath(exe);
        }
        catch
        {
            return null;
        }
    }

    private void NotifyLaunchProps()
    {
        OnPropertyChanged(nameof(LaunchButtonText));
        OnPropertyChanged(nameof(IsLaunchEnabled));
    }

    public void LaunchGame()
    {
        if (string.IsNullOrEmpty(GameDirectory))
            return;
        try
        {
            var exePath = Path.Combine(GameDirectory, "dota.exe");
            if (!File.Exists(exePath))
            {
                AppLog.Info($"LaunchGame: dota.exe not found at {exePath}");
                return;
            }

            AppLog.Info($"LaunchGame: starting {exePath}");
            using var _ = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = GameDirectory,
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            AppLog.Error("LaunchGame failed.", ex);
        }
        RefreshRunState();
    }

    public void OpenSettings() => IsSettingsOpen = true;
    public void CloseSettings() => IsSettingsOpen = false;

    public void SetGameDirectory(string? path)
    {
        GameDirectory = path;
        var settings = _settingsStorage.Get();
        settings.GameDirectory = path;
        _settingsStorage.Save(settings);
        OnPropertyChanged(nameof(IsGameDirectorySet));
        RefreshRunState();
        NotifyLaunchProps();
    }
}

