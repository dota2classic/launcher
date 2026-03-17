using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Api;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public enum GameCoordinatorState
{
    Disconnected,
    Connected,
    ConnectionComplete
}

public sealed class QueueSocketService : IQueueSocketService
{
    private const string DefaultSocketUrl = "wss://api.dotaclassic.ru";
    private const string SocketUrlEnvVar = "D2C_SOCKET_URL";

    private readonly ISocketFactory _socketFactory;
    private ISocket? _socket;
    private string? _token;

    public GameCoordinatorState State { get; private set; } = GameCoordinatorState.Disconnected;
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

    public QueueSocketService(ISocketFactory socketFactory)
    {
        _socketFactory = socketFactory;
    }

    public async Task ConnectAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        if (_socket is { Connected: true } && string.Equals(_token, token, StringComparison.Ordinal))
            return;

        await DisconnectAsync(cancellationToken).ConfigureAwait(false);

        _token = token;
        var (origin, path) = GetSocketEndpoint();

        var socket = _socketFactory.Create(origin, path, token);
        _socket = socket;

        socket.OnConnected += (_, _) => UpdateState(GameCoordinatorState.Connected);
        socket.OnDisconnected += (_, _) => UpdateState(GameCoordinatorState.Disconnected);

        socket.On("CONNECTION_COMPLETE", () => UpdateState(GameCoordinatorState.ConnectionComplete));
        socket.On<QueueStateMessage>("QUEUE_STATE", msg => QueueStateUpdated?.Invoke(msg));
        socket.On<PlayerQueueStateMessage>("PLAYER_QUEUE_STATE", msg => PlayerQueueStateUpdated?.Invoke(msg));
        socket.OnNullable<PlayerRoomStateMessage>("PLAYER_ROOM_STATE", msg =>
        {
            AppLog.Info($"PLAYER_ROOM_STATE: {msg}");
            PlayerRoomStateUpdated?.Invoke(msg);
        });
        socket.OnNullable<PlayerRoomStateMessage>("PLAYER_ROOM_FOUND", msg =>
        {
            AppLog.Info($"PLAYER_ROOM_FOUND: {msg}");
            PlayerRoomFound?.Invoke(msg);
            PlayerRoomStateUpdated?.Invoke(msg);
        });
        socket.On<PartyDto>("PLAYER_PARTY_STATE", msg => PartyUpdated?.Invoke(msg));
        socket.OnNullable<PlayerGameStateMessage>("PLAYER_GAME_STATE", msg => PlayerGameStateUpdated?.Invoke(msg));
        socket.OnNullable<PlayerGameStateMessage>("PLAYER_GAME_READY", msg => PlayerGameStateUpdated?.Invoke(msg));
        socket.On<PlayerServerSearchingMessage>("PLAYER_SERVER_SEARCHING", msg => ServerSearchingUpdated?.Invoke(msg));
        socket.On<OnlineUpdateMessage>("ONLINE_UPDATE", msg => OnlineUpdated?.Invoke(msg));
        socket.On<PlayerPartyInvitationsMessage>("PLAYER_PARTY_INVITES_STATE", msg => PartyInvitationsUpdated?.Invoke(msg));
        socket.On<PartyInviteReceivedMessage>("PARTY_INVITE_RECEIVED", msg => PartyInviteReceived?.Invoke(msg));
        socket.On<PartyInviteExpiredMessage>("PARTY_INVITE_EXPIRED", msg => PartyInviteExpired?.Invoke(msg));
        socket.On<NotificationCreatedMessage>("NOTIFICATION_CREATED", msg => NotificationCreated?.Invoke(msg));
        socket.On<PleaseEnterQueueMessage>("GO_QUEUE", msg => PleaseEnterQueue?.Invoke(msg));

        await socket.ConnectAsync().ConfigureAwait(false);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_socket == null)
            return;

        try
        {
            if (_socket.Connected)
                await _socket.DisconnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLog.Error("Queue socket disconnect failed.", ex);
        }
        finally
        {
            _socket.Dispose();
            _socket = null;
            _token = null;
            UpdateState(GameCoordinatorState.Disconnected);
        }
    }

    public Task EnterQueueAsync(MatchmakingMode[] modes, CancellationToken cancellationToken = default) =>
        EmitAsync("ENTER_QUEUE", new EnterQueueMessage(modes));

    public Task LeaveAllQueuesAsync(CancellationToken cancellationToken = default) =>
        EmitAsync("LEAVE_ALL_QUEUES");

    public Task SetReadyCheckAsync(string roomId, bool accept, CancellationToken cancellationToken = default) =>
        EmitAsync("SET_READY_CHECK", new SetReadyCheckMessage(roomId, accept));

    public Task InviteToPartyAsync(string invitedPlayerId, CancellationToken cancellationToken = default) =>
        EmitAsync("INVITE_TO_PARTY", new InviteToPartyMessage(invitedPlayerId));

    public Task AcceptPartyInviteAsync(string inviteId, bool accept, CancellationToken cancellationToken = default) =>
        EmitAsync("ACCEPT_PARTY_INVITE", new AcceptPartyInviteMessage(inviteId, accept));

    public Task LeavePartyAsync(CancellationToken cancellationToken = default) =>
        EmitAsync("LEAVE_PARTY");

    private Task EmitAsync(string eventName)
    {
        if (_socket == null || !_socket.Connected)
            return Task.CompletedTask;
        return _socket.EmitAsync(eventName);
    }

    private Task EmitAsync(string eventName, object payload)
    {
        if (_socket == null || !_socket.Connected)
            return Task.CompletedTask;
        return _socket.EmitAsync(eventName, payload);
    }

    private void UpdateState(GameCoordinatorState state)
    {
        if (State == state)
            return;
        State = state;
        StateChanged?.Invoke(state);
    }

    private static (string origin, string path) GetSocketEndpoint()
    {
        var url = Environment.GetEnvironmentVariable(SocketUrlEnvVar);
        if (string.IsNullOrWhiteSpace(url))
            url = DefaultSocketUrl;

        var uri = new Uri(url);
        var origin = uri.GetLeftPart(UriPartial.Authority);
        var path = uri.AbsolutePath == "/" ? "/socket.io" : uri.AbsolutePath;
        return (origin, path);
    }

    public void Dispose()
    {
        if (_socket == null)
            return;

        try
        {
            if (_socket.Connected)
                Task.Run(() => _socket.DisconnectAsync()).Wait(TimeSpan.FromSeconds(2));
        }
        catch { }

        _socket.Dispose();
        _socket = null;
    }
}

public sealed record QueueStateMessage(
    [property: JsonPropertyName("mode")] MatchmakingMode Mode,
    [property: JsonPropertyName("version")]
    [property: JsonConverter(typeof(JsonStringEnumConverter<Dota2Version>))] Dota2Version Version,
    [property: JsonPropertyName("inQueue")] int InQueue);

public sealed record PlayerQueueStateMessage(
    [property: JsonPropertyName("partyId")] string PartyId,
    [property: JsonPropertyName("modes")] MatchmakingMode[] Modes,
    [property: JsonPropertyName("inQueue")] bool InQueue);

public enum ReadyState
{
    Ready = 0,
    Decline = 1,
    Timeout = 2,
    Pending = 3
}

public sealed record PlayerRoomEntry(
    [property: JsonPropertyName("steamId")] string SteamId,
    [property: JsonPropertyName("state")] ReadyState State);

public sealed record PlayerRoomStateMessage(
    [property: JsonPropertyName("roomId")] string RoomId,
    [property: JsonPropertyName("mode")] MatchmakingMode Mode,
    [property: JsonPropertyName("entries")] PlayerRoomEntry[] Entries);

public sealed record PlayerGameStateMessage(
    [property: JsonPropertyName("serverUrl")] string ServerUrl);

public sealed record PlayerPartyInvitationsMessage(
    [property: JsonPropertyName("invitations")] PartyInviteReceivedMessage[] Invitations);

public sealed record PartyInviteReceivedMessage(
    [property: JsonPropertyName("partyId")] string PartyId,
    [property: JsonPropertyName("inviteId")] string InviteId,
    [property: JsonPropertyName("inviter")] UserDTO Inviter);

public sealed record PartyInviteExpiredMessage(
    [property: JsonPropertyName("inviteId")] string InviteId);

public sealed record PlayerServerSearchingMessage(
    [property: JsonPropertyName("searching")] bool Searching);

public sealed record OnlineUpdateMessage(
    [property: JsonPropertyName("online")] string[] Online,
    [property: JsonPropertyName("sessions")] int Sessions);

public sealed record PleaseEnterQueueMessage(
    [property: JsonPropertyName("mode")] MatchmakingMode Mode,
    [property: JsonPropertyName("version")]
    [property: JsonConverter(typeof(JsonStringEnumConverter<Dota2Version>))] Dota2Version Version,
    [property: JsonPropertyName("inQueue")] int InQueue);

public sealed record NotificationCreatedMessage(
    [property: JsonPropertyName("notificationDto")] NotificationDto NotificationDto);

public sealed record EnterQueueMessage(
    [property: JsonPropertyName("modes")] MatchmakingMode[] Modes);

public sealed record SetReadyCheckMessage(
    [property: JsonPropertyName("roomId")] string RoomId,
    [property: JsonPropertyName("accept")] bool Accept);

public sealed record InviteToPartyMessage(
    [property: JsonPropertyName("invitedPlayerId")] string InvitedPlayerId);

public sealed record AcceptPartyInviteMessage(
    [property: JsonPropertyName("inviteId")] string InviteId,
    [property: JsonPropertyName("accept")] bool Accept);
