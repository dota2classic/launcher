using System;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Api;

namespace d2c_launcher.Services;

public interface IQueueSocketService : IDisposable
{
    GameCoordinatorState State { get; }
    event Action<GameCoordinatorState>? StateChanged;
    event Action<PartyDto>? PartyUpdated;
    event Action<PlayerQueueStateMessage>? PlayerQueueStateUpdated;
    event Action<PlayerRoomStateMessage?>? PlayerRoomStateUpdated;
    event Action<PlayerRoomStateMessage?>? PlayerRoomFound;
    event Action<PlayerGameStateMessage?>? PlayerGameStateUpdated;
    event Action<QueueStateMessage>? QueueStateUpdated;
    event Action<PlayerServerSearchingMessage>? ServerSearchingUpdated;
    event Action<OnlineUpdateMessage>? OnlineUpdated;
    event Action<PartyInviteReceivedMessage>? PartyInviteReceived;
    event Action<PartyInviteExpiredMessage>? PartyInviteExpired;
    event Action<PlayerPartyInvitationsMessage>? PartyInvitationsUpdated;
    event Action<NotificationCreatedMessage>? NotificationCreated;
    event Action<PleaseEnterQueueMessage>? PleaseEnterQueue;

    Task ConnectAsync(string token, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task EnterQueueAsync(MatchmakingMode[] modes, CancellationToken cancellationToken = default);
    Task LeaveAllQueuesAsync(CancellationToken cancellationToken = default);
    Task SetReadyCheckAsync(string roomId, bool accept, CancellationToken cancellationToken = default);
    Task InviteToPartyAsync(string invitedPlayerId, CancellationToken cancellationToken = default);
    Task AcceptPartyInviteAsync(string inviteId, bool accept, CancellationToken cancellationToken = default);
    Task LeavePartyAsync(CancellationToken cancellationToken = default);
}
