using Avalonia.Threading;
using d2c_launcher.Services;
using d2c_launcher.Util;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Integration;

/// <summary>
/// Wires socket events to sound playback, floating invite notifications,
/// and window restoration from tray on match-critical events.
/// </summary>
public sealed class SocketSoundCoordinator
{
    public SocketSoundCoordinator(
        IQueueSocketService queueSocketService,
        NotificationAreaViewModel notificationArea,
        IWindowService windowService)
    {
        queueSocketService.PartyInviteReceived += msg =>
        {
            SoundPlayer.Play("party_invite.mp3");
            Dispatcher.UIThread.Post(() => notificationArea.AddInvite(msg));
        };

        queueSocketService.PartyInviteExpired += msg =>
            Dispatcher.UIThread.Post(() => notificationArea.RemoveByInviteId(msg.InviteId));

        queueSocketService.PlayerRoomFound += _ =>
        {
            SoundPlayer.Play("match_found.mp3");
            Dispatcher.UIThread.Post(windowService.ShowAndActivate);
        };

        queueSocketService.PlayerGameStateUpdated += msg =>
        {
            if (!string.IsNullOrEmpty(msg?.ServerUrl))
            {
                SoundPlayer.Play("ready_check_no_focus.wav");
                Dispatcher.UIThread.Post(windowService.ShowAndActivate);
            }
        };
    }
}
