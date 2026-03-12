using System;
using Avalonia.Threading;
using d2c_launcher.Services;
using d2c_launcher.Util;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Integration;

/// <summary>
/// Wires socket events to sound playback, floating invite notifications,
/// and window restoration from tray on match-critical events.
/// </summary>
public sealed class SocketSoundCoordinator : IDisposable
{
    private readonly IQueueSocketService _queueSocketService;
    private readonly Action<PartyInviteReceivedMessage> _onPartyInviteReceived;
    private readonly Action<PartyInviteExpiredMessage> _onPartyInviteExpired;
    private readonly Action<PlayerRoomStateMessage?> _onPlayerRoomFound;
    private readonly Action<PlayerGameStateMessage?> _onPlayerGameStateUpdated;

    public SocketSoundCoordinator(
        IQueueSocketService queueSocketService,
        NotificationAreaViewModel notificationArea,
        IWindowService windowService)
    {
        _queueSocketService = queueSocketService;

        _onPartyInviteReceived = msg =>
        {
            SoundPlayer.Play("party_invite.mp3");
            Dispatcher.UIThread.Post(() => notificationArea.AddInvite(msg));
        };
        _onPartyInviteExpired = msg =>
            Dispatcher.UIThread.Post(() => notificationArea.RemoveByInviteId(msg.InviteId));
        _onPlayerRoomFound = _ =>
        {
            SoundPlayer.Play("match_found.mp3");
            Dispatcher.UIThread.Post(windowService.ShowAndActivate);
        };
        _onPlayerGameStateUpdated = msg =>
        {
            if (!string.IsNullOrEmpty(msg?.ServerUrl))
            {
                SoundPlayer.Play("ready_check_no_focus.wav");
                Dispatcher.UIThread.Post(windowService.ShowAndActivate);
            }
        };

        queueSocketService.PartyInviteReceived += _onPartyInviteReceived;
        queueSocketService.PartyInviteExpired += _onPartyInviteExpired;
        queueSocketService.PlayerRoomFound += _onPlayerRoomFound;
        queueSocketService.PlayerGameStateUpdated += _onPlayerGameStateUpdated;
    }

    public void Dispose()
    {
        _queueSocketService.PartyInviteReceived -= _onPartyInviteReceived;
        _queueSocketService.PartyInviteExpired -= _onPartyInviteExpired;
        _queueSocketService.PlayerRoomFound -= _onPlayerRoomFound;
        _queueSocketService.PlayerGameStateUpdated -= _onPlayerGameStateUpdated;
    }
}
