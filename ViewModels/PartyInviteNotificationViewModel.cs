using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;

namespace d2c_launcher.ViewModels;

public sealed class PartyInviteNotificationViewModel : NotificationViewModel
{
    private const int TimeoutSeconds = 60;

    private readonly string _inviteId;
    private readonly Func<string, bool, Task> _respond;

    public string InviteId => _inviteId;
    public string InviterName { get; }
    public string? InviterAvatarUrl { get; }

    public IAsyncRelayCommand AcceptCommand { get; }
    public IAsyncRelayCommand DeclineCommand { get; }

    public PartyInviteNotificationViewModel(string inviteId, string inviterName, string? avatarUrl, Func<string, bool, Task> respond)
        : base(TimeoutSeconds, notificationId: inviteId)
    {
        _inviteId = inviteId;
        InviterName = inviterName;
        InviterAvatarUrl = avatarUrl;
        _respond = respond;

        AcceptCommand = new AsyncRelayCommand(() => RespondAsync(true));
        DeclineCommand = new AsyncRelayCommand(() => RespondAsync(false));
    }

    private async Task RespondAsync(bool accept)
    {
        StopTimer();
        try { await _respond(_inviteId, accept).ConfigureAwait(false); } catch { }
        if (accept)
            Services.FaroTelemetryService.TrackEvent("party_invite_accepted");
        Dispatcher.UIThread.Post(() => ForceClose());
    }
}
