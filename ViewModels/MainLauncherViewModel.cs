using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    private CancellationTokenSource? _ticketExchangeCts;
    private CancellationTokenSource? _inviteSearchCts;
    private int _partyRefreshRunning;
    private HashSet<string> _onlineUsers = new(StringComparer.Ordinal);

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

    public IRelayCommand CloseInviteModalCommand { get; }

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
        _queueSocketService.OnlineUpdated += msg => Dispatcher.UIThread.Post(() => UpdateOnlineUsers(msg));

        CloseInviteModalCommand = new RelayCommand(CloseInviteModal);

        if (!string.IsNullOrWhiteSpace(_backendAccessToken))
            _ = EnsureQueueConnectionAsync(_backendAccessToken, CancellationToken.None);

        _ = RefreshPartyAsync();
        _ = RefreshMatchmakingModesAsync();
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

            var members = await _backendApiService.GetMyPartyAsync(BackendAccessToken);
            DisposePartyAvatars();
            PartyMembers.Clear();
            foreach (var m in members)
                PartyMembers.Add(m);

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
            SearchingModesText = "Не в поиске";
        }

        IsSearching = msg.InQueue;
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

    /// <summary>Same command as dota684.bat so process tree and context match double‑clicking the bat.</summary>
    private const string LauncherBatchFileName = "dota684.bat";
    private const string GameLaunchArguments = "";

    public void LaunchGame()
    {
        if (string.IsNullOrEmpty(GameDirectory))
            return;
        try
        {
            var proxyExe = Path.Combine(AppContext.BaseDirectory, "d2c-launch-proxy.exe");
            if (!File.Exists(proxyExe))
                return;

            var launcherBat = Path.Combine(GameDirectory, LauncherBatchFileName);
            if (File.Exists(launcherBat))
            {
                using var _ = Process.Start(new ProcessStartInfo
                {
                    FileName = proxyExe,
                    WorkingDirectory = GameDirectory,
                    UseShellExecute = false,
                    Arguments = "\"" + launcherBat + "\" \"" + GameDirectory + "\""
                });
            }
            else
            {
                var exePath = Path.Combine(GameDirectory, "dota.exe");
                var launchArgs = string.IsNullOrWhiteSpace(GameLaunchArguments) ? "" : GameLaunchArguments;
                using var _ = Process.Start(new ProcessStartInfo
                {
                    FileName = proxyExe,
                    WorkingDirectory = GameDirectory,
                    UseShellExecute = false,
                    Arguments = "\"" + exePath + "\" \"" + GameDirectory + "\" \"" + launchArgs + "\""
                });
            }
        }
        catch
        {
            // Ignore
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
