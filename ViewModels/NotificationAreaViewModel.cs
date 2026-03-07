using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

public sealed class NotificationAreaViewModel
{
    private readonly IQueueSocketService _queueSocketService;

    public ObservableCollection<PartyInviteNotificationViewModel> Invites { get; } = new();

    public NotificationAreaViewModel(IQueueSocketService queueSocketService)
    {
        _queueSocketService = queueSocketService;
    }

    public void AddInvite(Services.PartyInviteReceivedMessage msg)
    {
        // Don't show duplicates
        if (Invites.Any(v => v.InviteId == msg.InviteId))
            return;

        var avatarUrl = msg.Inviter?.AvatarSmall ?? msg.Inviter?.Avatar;
        var vm = new PartyInviteNotificationViewModel(
            msg.InviteId,
            msg.Inviter?.Name ?? "Unknown",
            avatarUrl,
            (id, accept) => _queueSocketService.AcceptPartyInviteAsync(id, accept));

        vm.Closed += v => Dispatcher.UIThread.Post(() => Invites.Remove(v));

        Invites.Add(vm);
    }

    public void RemoveByInviteId(string inviteId)
    {
        var vm = Invites.FirstOrDefault(v => v.InviteId == inviteId);
        vm?.ForceClose();
    }
}
