using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using d2c_launcher.Api;
using d2c_launcher.Resources;
using d2c_launcher.Services;
using d2c_launcher.Util;
using d2c_launcher.ViewModels;
using NotificationType = d2c_launcher.Api.NotificationType;

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
    private readonly IBackendApiService _backendApiService;
    private readonly IToastNotificationService _toastService;
    private readonly Func<MatchmakingMode, string> _getModeName;
    private readonly ISettingsStorage _settingsStorage;

    private string? _lastServerUrl;

    public SocketEventCoordinator(
        IQueueSocketService queueSocketService,
        NotificationAreaViewModel notificationArea,
        IWindowService windowService,
        IBackendApiService backendApiService,
        IToastNotificationService toastService,
        Func<MatchmakingMode, string> getModeName,
        ISettingsStorage settingsStorage)
    {
        _queueSocketService = queueSocketService;
        _notificationArea = notificationArea;
        _windowService = windowService;
        _backendApiService = backendApiService;
        _toastService = toastService;
        _getModeName = getModeName;
        _settingsStorage = settingsStorage;

        queueSocketService.PartyInviteReceived += OnPartyInviteReceived;
        queueSocketService.PartyInviteExpired += OnPartyInviteExpired;
        queueSocketService.PlayerRoomFound += OnPlayerRoomFound;
        queueSocketService.PlayerGameStateUpdated += OnPlayerGameStateUpdated;
        queueSocketService.PleaseEnterQueue += OnPleaseEnterQueue;
        queueSocketService.NotificationCreated += OnNotificationCreated;
    }

    private float NotificationVolume => _settingsStorage.Get().NotificationSoundVolume;

    private void OnPartyInviteReceived(PartyInviteReceivedMessage msg)
    {
        SoundPlayer.Play("party_invite.mp3", volume: NotificationVolume);
        Dispatcher.UIThread.Post(() => _notificationArea.AddInvite(msg));
        if (!_windowService.IsWindowActive)
            Dispatcher.UIThread.Post(() => _toastService.ShowPartyInvite(msg.InviteId, msg.Inviter?.Name ?? "Unknown"));
    }

    private void OnPartyInviteExpired(PartyInviteExpiredMessage msg) =>
        Dispatcher.UIThread.Post(() => _notificationArea.RemoveByInviteId(msg.InviteId));

    private void OnPlayerRoomFound(PlayerRoomStateMessage? msg)
    {
        SoundPlayer.Play("match_found.mp3");
        if (_windowService.IsWindowVisible)
            Dispatcher.UIThread.Post(_windowService.ShowAndActivate);
        else if (!string.IsNullOrWhiteSpace(msg?.RoomId))
            Dispatcher.UIThread.Post(() => _toastService.ShowMatchFound(msg.RoomId));
    }

    private void OnPlayerGameStateUpdated(PlayerGameStateMessage? msg)
    {
        var serverUrl = msg?.ServerUrl;
        if (string.IsNullOrEmpty(serverUrl) || serverUrl == _lastServerUrl)
            return;

        _lastServerUrl = serverUrl;
        Dispatcher.UIThread.Post(_windowService.ShowAndActivate);
    }

    private void OnPleaseEnterQueue(PleaseEnterQueueMessage msg)
    {
        SoundPlayer.Play("party_invite.mp3", volume: NotificationVolume);
        var title = string.Format(Strings.GoQueueTitle, _getModeName(msg.Mode));
        var content = string.Format(Strings.GoQueueContent, msg.InQueue);
        Dispatcher.UIThread.Post(() => _notificationArea.AddGoQueueToast(title, content));
        if (!_windowService.IsWindowActive)
            Dispatcher.UIThread.Post(() => _toastService.ShowGoQueue(title, content, (int)msg.Mode));
    }

    private void OnNotificationCreated(NotificationCreatedMessage msg)
    {
        var notification = msg.NotificationDto;
        if (notification.NotificationType == NotificationType.ACHIEVEMENT_COMPLETE)
            Dispatcher.UIThread.Post(() => _notificationArea.AddAchievementToast(notification, _backendApiService));
    }

    /// <summary>
    /// Fetches all pending (unacknowledged) notifications from the API and processes each
    /// through <see cref="OnNotificationCreated"/>, so toasts are shown on startup.
    /// </summary>
    public async Task LoadPendingNotificationsAsync()
    {
        var notifications = await _backendApiService.GetNotificationsAsync().ConfigureAwait(false);
        foreach (var notification in notifications)
            OnNotificationCreated(new NotificationCreatedMessage(notification));
    }

    public void Dispose()
    {
        _queueSocketService.PartyInviteReceived -= OnPartyInviteReceived;
        _queueSocketService.PartyInviteExpired -= OnPartyInviteExpired;
        _queueSocketService.PlayerRoomFound -= OnPlayerRoomFound;
        _queueSocketService.PlayerGameStateUpdated -= OnPlayerGameStateUpdated;
        _queueSocketService.PleaseEnterQueue -= OnPleaseEnterQueue;
        _queueSocketService.NotificationCreated -= OnNotificationCreated;
    }
}
