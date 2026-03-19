using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using d2c_launcher.Api;
using d2c_launcher.Resources;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

public sealed class NotificationAreaViewModel
{
    private readonly IQueueSocketService _queueSocketService;

    public ObservableCollection<NotificationViewModel> Notifications { get; } = new();

    public NotificationAreaViewModel(IQueueSocketService queueSocketService)
    {
        _queueSocketService = queueSocketService;
    }

    public void AddInvite(PartyInviteReceivedMessage msg)
    {
        // Don't show duplicates
        if (Notifications.OfType<PartyInviteNotificationViewModel>().Any(v => v.InviteId == msg.InviteId))
            return;

        var avatarUrl = msg.Inviter?.AvatarSmall ?? msg.Inviter?.Avatar;
        var vm = new PartyInviteNotificationViewModel(
            msg.InviteId,
            msg.Inviter?.Name ?? "Unknown",
            avatarUrl,
            (id, accept) => _queueSocketService.AcceptPartyInviteAsync(id, accept));

        vm.Closed += v => Dispatcher.UIThread.Post(() => Notifications.Remove(v));
        Notifications.Add(vm);
    }

    public void RemoveByInviteId(string inviteId)
    {
        var vm = Notifications.OfType<PartyInviteNotificationViewModel>()
            .FirstOrDefault(v => v.InviteId == inviteId);
        vm?.ForceClose();
    }

    /// <summary>Shows a simple text toast that auto-dismisses.</summary>
    public void AddToast(string message, int displaySeconds = 4) =>
        AddNotification(new SimpleToastViewModel(message, displaySeconds));

    /// <summary>Shows a GO_QUEUE toast with a title and body line.</summary>
    public void AddGoQueueToast(string title, string content) =>
        AddNotification(new GoQueueToastViewModel(title, content));

    /// <summary>Shows the "corrupted files" toast with a verify-integrity button.</summary>
    public void AddCorruptedFilesToast(Action onVerify) =>
        AddNotification(new CorruptedFilesToastViewModel(onVerify));

    /// <summary>Shows an invite-sent toast (with player avatar) that auto-dismisses.</summary>
    public void AddInviteSentToast(InviteSentToastViewModel vm) => AddNotification(vm);

    /// <summary>Shows an achievement-unlocked toast that opens the website achievements page on click.</summary>
    public void AddAchievementToast(NotificationDto notification, IBackendApiService api)
    {
        var achievementKey = notification.Achievement != null ? (int)notification.Achievement.Key : -1;
        var vm = new AchievementToastViewModel(
            notification.Id,
            notification.SteamId,
            notification.Title,
            notification.Content,
            achievementKey,
            api);
        AddNotification(vm);
    }

    private void AddNotification(NotificationViewModel vm)
    {
        vm.Closed += v => Dispatcher.UIThread.Post(() => Notifications.Remove(v));
        Dispatcher.UIThread.Post(() => Notifications.Add(vm));
    }
}
