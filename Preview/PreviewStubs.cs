using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using d2c_launcher.Api;
using d2c_launcher.Models;
using d2c_launcher.Services;

namespace d2c_launcher.Preview;

internal sealed class StubQueueSocketService : IQueueSocketService
{
    public GameCoordinatorState State => GameCoordinatorState.Disconnected;
    public event Action<GameCoordinatorState>? StateChanged;
    public event Action<PartyDto>? PartyUpdated;
    public event Action<PlayerQueueStateMessage>? PlayerQueueStateUpdated;
    public event Action<PlayerRoomStateMessage?>? PlayerRoomStateUpdated;
    public event Action<PlayerRoomStateMessage?>? PlayerRoomFound;
    public event Action<PlayerGameStateMessage?>? PlayerGameStateUpdated;
    public event Action<QueueStateMessage>? QueueStateUpdated;
    public event Action<PlayerServerSearchingMessage>? ServerSearchingUpdated;
    public event Action<OnlineUpdateMessage>? OnlineUpdated;
    public event Action<PartyInviteReceivedMessage>? PartyInviteReceived;
    public event Action<PartyInviteExpiredMessage>? PartyInviteExpired;
    public event Action<PlayerPartyInvitationsMessage>? PartyInvitationsUpdated;
    public event Action<NotificationCreatedMessage>? NotificationCreated;
    public event Action<PleaseEnterQueueMessage>? PleaseEnterQueue;

    public Task ConnectAsync(string token, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task EnterQueueAsync(MatchmakingMode[] modes, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LeaveAllQueuesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetReadyCheckAsync(string roomId, bool accept, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task InviteToPartyAsync(string invitedPlayerId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AcceptPartyInviteAsync(string inviteId, bool accept, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LeavePartyAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Dispose() { }
}

internal sealed class StubBackendApiService : IBackendApiService
{
    public Task<PartySnapshot> GetMyPartySnapshotAsync(string bearerToken, CancellationToken cancellationToken = default)
        => Task.FromResult(new PartySnapshot([
            new PartyMemberView("76561198000000001", "Player One", null, null),
            new PartyMemberView("76561198000000002", "Player Two", null, null),
        ], null));

    public Task<IReadOnlyList<MatchmakingModeInfo>> GetEnabledMatchmakingModesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MatchmakingModeInfo>>([
            new MatchmakingModeInfo(8,  "Highroom 5x5"),
            new MatchmakingModeInfo(1,  "Обычная 5x5"),
            new MatchmakingModeInfo(13, "Турбо"),
            new MatchmakingModeInfo(2,  "1x1 Мид"),
            new MatchmakingModeInfo(7,  "Против ботов"),
        ]);

    public Task<IReadOnlyList<InviteCandidateView>> SearchPlayersAsync(string name, int count = 25, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<InviteCandidateView>>([]);

    public Task<(string? Name, Bitmap? AvatarImage)?> GetUserInfoAsync(string steamId, string bearerToken, CancellationToken cancellationToken = default)
        => Task.FromResult<(string?, Bitmap?)?>(null);

    public Task<(int InGame, int OnSite)> GetOnlineStatsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult((0, 0));

    public Task<Bitmap?> LoadAvatarFromUrlAsync(string? url, CancellationToken cancellationToken = default)
        => Task.FromResult<Bitmap?>(null);
}

internal sealed class StubSettingsStorage : ISettingsStorage
{
    public LauncherSettings Get() => new();
    public void Save(LauncherSettings settings) { }
}

internal sealed class StubSteamAuthApi : ISteamAuthApi
{
    public Task<string?> ExchangeSteamSessionTicketAsync(string ticket, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}
