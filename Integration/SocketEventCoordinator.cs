using System;
using Avalonia.Threading;
using d2c_launcher.Api;
using d2c_launcher.Resources;
using d2c_launcher.Services;
using d2c_launcher.Util;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Integration;

/// <summary>
/// Translates raw socket events from <see cref="IQueueSocketService"/> into user-visible
/// side-effects: sound playback, floating toast/notification area updates, and window
/// restoration from tray.
///
/// <list type="bullet">
///   <item><term>PARTY_INVITE_RECEIVED</term><description>Plays invite sound; shows party-invite notification card.</description></item>
///   <item><term>PARTY_INVITE_EXPIRED</term><description>Removes the matching invite card.</description></item>
///   <item><term>PLAYER_ROOM_FOUND</term><description>Plays match-found sound; restores window from tray.</description></item>
///   <item><term>PLAYER_GAME_STATE (new server URL)</term><description>Plays ready-check sound; restores window from tray.</description></item>
///   <item><term>GO_QUEUE</term><description>Plays invite sound; shows a titled toast with the mode name and current queue size.</description></item>
/// </list>
/// </summary>
public sealed class SocketEventCoordinator : IDisposable
{
    private readonly IQueueSocketService _queueSocketService;
    private readonly NotificationAreaViewModel _notificationArea;
    private readonly IWindowService _windowService;
    private readonly Func<MatchmakingMode, string> _getModeName;

    private string? _lastServerUrl;

    public SocketEventCoordinator(
        IQueueSocketService queueSocketService,
        NotificationAreaViewModel notificationArea,
        IWindowService windowService,
        Func<MatchmakingMode, string> getModeName)
    {
        _queueSocketService = queueSocketService;
        _notificationArea = notificationArea;
        _windowService = windowService;
        _getModeName = getModeName;

        queueSocketService.PartyInviteReceived += OnPartyInviteReceived;
        queueSocketService.PartyInviteExpired += OnPartyInviteExpired;
        queueSocketService.PlayerRoomFound += OnPlayerRoomFound;
        queueSocketService.PlayerGameStateUpdated += OnPlayerGameStateUpdated;
        queueSocketService.PleaseEnterQueue += OnPleaseEnterQueue;
    }

    private void OnPartyInviteReceived(PartyInviteReceivedMessage msg)
    {
        SoundPlayer.Play("party_invite.mp3");
        Dispatcher.UIThread.Post(() => _notificationArea.AddInvite(msg));
    }

    private void OnPartyInviteExpired(PartyInviteExpiredMessage msg) =>
        Dispatcher.UIThread.Post(() => _notificationArea.RemoveByInviteId(msg.InviteId));

    private void OnPlayerRoomFound(PlayerRoomStateMessage? _)
    {
        SoundPlayer.Play("match_found.mp3");
        Dispatcher.UIThread.Post(_windowService.ShowAndActivate);
    }

    private void OnPlayerGameStateUpdated(PlayerGameStateMessage? msg)
    {
        var serverUrl = msg?.ServerUrl;
        if (string.IsNullOrEmpty(serverUrl) || serverUrl == _lastServerUrl)
            return;

        _lastServerUrl = serverUrl;
        SoundPlayer.Play("ready_check_no_focus.wav");
        Dispatcher.UIThread.Post(_windowService.ShowAndActivate);
    }

    private void OnPleaseEnterQueue(PleaseEnterQueueMessage msg)
    {
        SoundPlayer.Play("party_invite.mp3");
        var title = string.Format(Strings.GoQueueTitle, _getModeName(msg.Mode));
        var content = string.Format(Strings.GoQueueContent, msg.InQueue);
        Dispatcher.UIThread.Post(() => _notificationArea.AddGoQueueToast(title, content));
    }

    public void Dispose()
    {
        _queueSocketService.PartyInviteReceived -= OnPartyInviteReceived;
        _queueSocketService.PartyInviteExpired -= OnPartyInviteExpired;
        _queueSocketService.PlayerRoomFound -= OnPlayerRoomFound;
        _queueSocketService.PlayerGameStateUpdated -= OnPlayerGameStateUpdated;
        _queueSocketService.PleaseEnterQueue -= OnPleaseEnterQueue;
    }
}
