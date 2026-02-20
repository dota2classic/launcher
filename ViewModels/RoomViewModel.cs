using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Api;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class RoomViewModel : ViewModelBase
{
    private readonly IQueueSocketService _queueSocketService;
    private readonly IBackendApiService _backendApiService;

    // Delegates set by parent after construction
    public Func<Models.User?> GetCurrentUser { get; set; } = () => null;
    public Func<string?> GetBackendToken { get; set; } = () => null;
    public Func<MatchmakingMode, string> GetModeName { get; set; } = m => m.ToString();

    [ObservableProperty]
    private bool _isAcceptGameModalOpen;

    [ObservableProperty]
    private bool _isServerSearchingModalOpen;

    [ObservableProperty]
    private bool _hasMyPlayerAccepted;

    [ObservableProperty]
    private bool _hasMyPlayerResponded;

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

    public RoomViewModel(IQueueSocketService queueSocketService, IBackendApiService backendApiService)
    {
        _queueSocketService = queueSocketService;
        _backendApiService = backendApiService;

        queueSocketService.PlayerRoomStateUpdated += msg =>
            Dispatcher.UIThread.Post(() => _ = UpdatePlayerRoomStateAsync(msg));
        queueSocketService.PlayerGameStateUpdated += msg =>
            Dispatcher.UIThread.Post(() => UpdatePlayerGameState(msg));
        queueSocketService.ServerSearchingUpdated += msg =>
            Dispatcher.UIThread.Post(() => UpdateServerSearching(msg));

        AcceptGameCommand = new RelayCommand(AcceptGame);
        DeclineGameCommand = new RelayCommand(DeclineGame);
    }

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
            AppLog.Info("UpdatePlayerGameState (room): no action needed");
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
            AppLog.Info("UpdatePlayerRoomStateAsync: msg=null (room cleared)");
            ClearRoomState();
            return;
        }

        AppLog.Info($"UpdatePlayerRoomStateAsync: roomId={msg.RoomId}, mode={msg.Mode}, entries={msg.Entries?.Length ?? 0}");
        foreach (var e in msg.Entries ?? Array.Empty<PlayerRoomEntry>())
            AppLog.Info($"  Entry: steamId={e.SteamId}, state={e.State}");

        CurrentRoomId = msg.RoomId;
        RoomMode = msg.Mode;

        // Remove players no longer in room
        var steamIds = msg.Entries.Select(e => e.SteamId).ToHashSet(StringComparer.Ordinal);
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
                        info?.AvatarImage?.Dispose();
                        return;
                    }
                    RoomPlayers.Add(new RoomPlayerView(
                        entry.SteamId,
                        info?.Name ?? entry.SteamId,
                        info?.AvatarImage,
                        entry.State));
                }
                catch (Exception ex)
                {
                    AppLog.Error($"Failed to fetch user info for {entry.SteamId}", ex);
                    if (CurrentRoomId != msg.RoomId)
                        return;
                    RoomPlayers.Add(new RoomPlayerView(entry.SteamId, entry.SteamId, null, entry.State));
                }
            }
        }

        // Backend uses SteamID3 (32-bit account ID); local SteamID64 → lower 32 bits
        var currentUser = GetCurrentUser();
        var myId = currentUser != null
            ? ((uint)(currentUser.SteamId & 0xFFFFFFFF)).ToString()
            : null;
        var myEntry = myId != null ? msg.Entries.FirstOrDefault(e => e.SteamId == myId) : null;
        HasMyPlayerAccepted = myEntry?.State == ReadyState.Ready;
        HasMyPlayerResponded = myEntry != null && myEntry.State != ReadyState.Pending;

        if (!IsAcceptGameModalOpen)
            IsAcceptGameModalOpen = true;
    }

    private async Task<(string? Name, Bitmap? AvatarImage)?> FetchUserInfoAsync(string steamId)
    {
        var token = GetBackendToken();
        if (string.IsNullOrWhiteSpace(token))
            return null;
        return await _backendApiService.GetUserInfoAsync(steamId, token);
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
        try
        {
            await _queueSocketService.SetReadyCheckAsync(CurrentRoomId, false);
            AppLog.Info($"Declined game for room: {CurrentRoomId}");
            ClearRoomState();
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to decline game.", ex);
        }
    }

    private void ClearRoomState()
    {
        IsAcceptGameModalOpen = false;
        HasMyPlayerAccepted = false;
        HasMyPlayerResponded = false;
        CurrentRoomId = null;
        RoomMode = null;
        foreach (var player in RoomPlayers)
            player.AvatarImage?.Dispose();
        RoomPlayers.Clear();
    }
}
