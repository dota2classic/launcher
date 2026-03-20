using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Api;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class RoomViewModel : ViewModelBase, IDisposable
{
    private readonly IQueueSocketService _queueSocketService;
    private readonly IBackendApiService _backendApiService;

    // Delegates set by parent after construction
    public Func<Models.User?> GetCurrentUser { get; set; } = () => null;
    public Func<MatchmakingMode, string> GetModeName { get; set; } = m => m.ToString();

    [ObservableProperty]
    private bool _isAcceptGameModalOpen;

    [ObservableProperty]
    private bool _isTimeoutModalOpen;

    [ObservableProperty]
    private bool _isServerSearchingModalOpen;

    [ObservableProperty]
    private bool _hasMyPlayerAccepted;

    [ObservableProperty]
    private bool _hasMyPlayerResponded;

    private ReadyState? _myLastState;
    private bool _isDeclinePending;

    [ObservableProperty]
    private ObservableCollection<RoomPlayerView> _roomPlayers = new();

    [ObservableProperty]
    private string? _currentRoomId;

    [ObservableProperty]
    private MatchmakingMode? _roomMode;

    public string RoomModeText => RoomMode.HasValue ? GetModeName(RoomMode.Value) : "";

    partial void OnRoomModeChanged(MatchmakingMode? value) => OnPropertyChanged(nameof(RoomModeText));

    public IRelayCommand AcceptGameCommand { get; }
    public IRelayCommand DeclineGameCommand { get; }
    public IRelayCommand CloseTimeoutModalCommand { get; }

    public RoomViewModel(IQueueSocketService queueSocketService, IBackendApiService backendApiService)
    {
        _queueSocketService = queueSocketService;
        _backendApiService = backendApiService;

        queueSocketService.PlayerRoomStateUpdated += OnPlayerRoomStateUpdated;
        queueSocketService.PlayerGameStateUpdated += OnPlayerGameStateUpdated;
        queueSocketService.ServerSearchingUpdated += OnServerSearchingUpdated;

        AcceptGameCommand = new RelayCommand(AcceptGame);
        DeclineGameCommand = new RelayCommand(DeclineGame);
        CloseTimeoutModalCommand = new RelayCommand(() => IsTimeoutModalOpen = false);
    }

    private void OnPlayerRoomStateUpdated(PlayerRoomStateMessage? msg) =>
        Dispatcher.UIThread.Post(() => _ = UpdatePlayerRoomStateAsync(msg));

    private void OnPlayerGameStateUpdated(PlayerGameStateMessage? msg) =>
        Dispatcher.UIThread.Post(() => UpdatePlayerGameState(msg));

    private void OnServerSearchingUpdated(PlayerServerSearchingMessage msg) =>
        Dispatcher.UIThread.Post(() => UpdateServerSearching(msg));

    private void UpdateServerSearching(PlayerServerSearchingMessage msg)
    {
        AppLog.Info($"UpdateServerSearching: searching={msg.Searching}");
        IsServerSearchingModalOpen = msg.Searching;
        if (msg.Searching)
            IsAcceptGameModalOpen = true;
    }

    private void UpdatePlayerGameState(PlayerGameStateMessage? msg)
    {
        if (msg == null)
        {
            return;
        }

        AppLog.Info($"UpdatePlayerGameState (room): closing modals, serverUrl={msg.ServerUrl}");
        IsAcceptGameModalOpen = false;
        IsServerSearchingModalOpen = false;
    }

    private async Task UpdatePlayerRoomStateAsync(PlayerRoomStateMessage? msg)
    {
        if (msg == null)
        {
            AppLog.Info($"UpdatePlayerRoomStateAsync: msg=null (room cleared), myLastState={_myLastState}");
            if ((_myLastState is ReadyState.Pending or ReadyState.Timeout) && !_isDeclinePending)
                IsTimeoutModalOpen = true;
            ClearRoomState();
            return;
        }

        AppLog.Info($"UpdatePlayerRoomStateAsync: roomId={msg.RoomId}, mode={msg.Mode}, entries={msg.Entries?.Length ?? 0}");

        IsTimeoutModalOpen = false;
        CurrentRoomId = msg.RoomId;
        RoomMode = msg.Mode;

        // Remove players no longer in room
        var steamIds = (msg.Entries ?? []).Select(e => e.SteamId).ToHashSet(StringComparer.Ordinal);
        for (int i = RoomPlayers.Count - 1; i >= 0; i--)
        {
            if (!steamIds.Contains(RoomPlayers[i].SteamId))
                RoomPlayers.RemoveAt(i);
        }

        // Update or add players
        foreach (var entry in msg.Entries ?? [])
        {
            var existing = RoomPlayers.FirstOrDefault(p => p.SteamId == entry.SteamId);
            if (existing != null)
            {
                existing.State = entry.State;
            }
            else
            {
                try
                {
                    var info = await FetchUserInfoAsync(entry.SteamId);
                    if (CurrentRoomId != msg.RoomId)
                    {
                        AppLog.Info($"UpdatePlayerRoomStateAsync: room changed during fetch, aborting (expected={msg.RoomId}, current={CurrentRoomId})");
                        return;
                    }
                    // Re-check after await: a concurrent update may have already added this player
                    var existingAfterFetch = RoomPlayers.FirstOrDefault(p => p.SteamId == entry.SteamId);
                    if (existingAfterFetch != null)
                    {
                        existingAfterFetch.State = entry.State;
                    }
                    else
                    {
                        RoomPlayers.Add(new RoomPlayerView(
                            entry.SteamId,
                            info?.Name ?? entry.SteamId,
                            info?.AvatarUrl,
                            entry.State));
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Error($"Failed to fetch user info for {entry.SteamId}", ex);
                    if (CurrentRoomId != msg.RoomId)
                        return;
                    if (!RoomPlayers.Any(p => p.SteamId == entry.SteamId))
                        RoomPlayers.Add(new RoomPlayerView(entry.SteamId, entry.SteamId, null, entry.State));
                }
            }
        }

        var currentUser = GetCurrentUser();
        var myId = currentUser?.SteamId32.ToString();
        var myEntry = myId != null ? (msg.Entries ?? []).FirstOrDefault(e => e.SteamId == myId) : null;
        _myLastState = myEntry?.State;
        HasMyPlayerAccepted = myEntry?.State == ReadyState.Ready;
        HasMyPlayerResponded = myEntry != null && myEntry.State != ReadyState.Pending;

        if (!IsAcceptGameModalOpen)
            IsAcceptGameModalOpen = true;
    }

    private async Task<(string? Name, string? AvatarUrl)?> FetchUserInfoAsync(string steamId)
    {
        return await _backendApiService.GetUserInfoAsync(steamId);
    }

    private void AcceptGame()
    {
        if (!string.IsNullOrWhiteSpace(CurrentRoomId))
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
            d2c_launcher.Services.FaroTelemetryService.TrackEvent("game_accepted");
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to accept game.", ex);
        }
    }

    private void DeclineGame()
    {
        if (!string.IsNullOrWhiteSpace(CurrentRoomId))
            _ = DeclineGameAsync();
    }

    private async Task DeclineGameAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentRoomId))
            return;
        _isDeclinePending = true;
        try
        {
            await _queueSocketService.SetReadyCheckAsync(CurrentRoomId, false);
            AppLog.Info($"Declined game for room: {CurrentRoomId}");
            d2c_launcher.Services.FaroTelemetryService.TrackEvent("game_declined");
            ClearRoomState();
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to decline game.", ex);
        }
        finally
        {
            _isDeclinePending = false;
        }
    }

    private void ClearRoomState()
    {
        IsAcceptGameModalOpen = false;
        HasMyPlayerAccepted = false;
        HasMyPlayerResponded = false;
        CurrentRoomId = null;
        RoomMode = null;
        RoomPlayers.Clear();
        _myLastState = null;
    }

    public void Dispose()
    {
        _queueSocketService.PlayerRoomStateUpdated -= OnPlayerRoomStateUpdated;
        _queueSocketService.PlayerGameStateUpdated -= OnPlayerGameStateUpdated;
        _queueSocketService.ServerSearchingUpdated -= OnServerSearchingUpdated;
    }
}
