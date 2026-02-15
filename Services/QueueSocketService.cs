using System;
using System.Collections.Generic;
using System.Threading;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using d2c_launcher.Api;
using d2c_launcher.Util;
using SocketIOClient;
using SocketIOClient.Transport;

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
    private SocketIOClient.SocketIO? _socket;
    private string? _token;

    public GameCoordinatorState State { get; private set; } = GameCoordinatorState.Disconnected;
    public event Action<GameCoordinatorState>? StateChanged;
    public event Action<PartyDto>? PartyUpdated;
    public event Action<PlayerQueueStateMessage>? PlayerQueueStateUpdated;
    public event Action<PlayerRoomStateMessage?>? PlayerRoomStateUpdated;
    public event Action<PlayerGameStateMessage?>? PlayerGameStateUpdated;
    public event Action<QueueStateMessage>? QueueStateUpdated;
    public event Action<PlayerServerSearchingMessage>? ServerSearchingUpdated;
    public event Action<OnlineUpdateMessage>? OnlineUpdated;
    public event Action<PartyInviteReceivedMessage>? PartyInviteReceived;
    public event Action<PartyInviteExpiredMessage>? PartyInviteExpired;
    public event Action<PlayerPartyInvitationsMessage>? PartyInvitationsUpdated;
    public event Action<NotificationCreatedMessage>? NotificationCreated;
    public event Action<PleaseEnterQueueMessage>? PleaseEnterQueue;

    public async Task ConnectAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        if (_socket is { Connected: true } && string.Equals(_token, token, StringComparison.Ordinal))
            return;

        await DisconnectAsync(cancellationToken).ConfigureAwait(false);

        _token = token;
        var (origin, path) = GetSocketEndpoint();

        var options = new SocketIOOptions
        {
            Path = path,
            Transport = TransportProtocol.WebSocket,
            AutoUpgrade = true,
            ConnectionTimeout = TimeSpan.FromSeconds(10),
            Auth = new Dictionary<string, string>
            {
                ["token"] = token
            }
        };

        var socket = new SocketIOClient.SocketIO(origin, options);
        _socket = socket;

        socket.OnConnected += (_, _) =>
        {
            UpdateState(GameCoordinatorState.Connected);
        };

        socket.OnDisconnected += (_, _) =>
        {
            UpdateState(GameCoordinatorState.Disconnected);
        };

        socket.On("CONNECTION_COMPLETE", _ => UpdateState(GameCoordinatorState.ConnectionComplete));
        socket.On("QUEUE_STATE", response => RaiseParsed("QUEUE_STATE", response, QueueStateUpdated));
        socket.On("PLAYER_QUEUE_STATE", response => RaiseParsed("PLAYER_QUEUE_STATE", response, PlayerQueueStateUpdated));
        socket.On("PLAYER_ROOM_STATE", response => RaiseParsed("PLAYER_ROOM_STATE", response, PlayerRoomStateUpdated));
        socket.On("PLAYER_ROOM_FOUND", response => RaiseParsed("PLAYER_ROOM_FOUND", response, PlayerRoomStateUpdated));
        socket.On("PLAYER_PARTY_STATE", response => RaiseParsed("PLAYER_PARTY_STATE", response, PartyUpdated));
        socket.On("PLAYER_GAME_STATE", response => RaiseParsed("PLAYER_GAME_STATE", response, PlayerGameStateUpdated));
        socket.On("PLAYER_GAME_READY", response => RaiseParsed("PLAYER_GAME_READY", response, PlayerGameStateUpdated));
        socket.On("PLAYER_SERVER_SEARCHING", response => RaiseParsed("PLAYER_SERVER_SEARCHING", response, ServerSearchingUpdated));
        socket.On("ONLINE_UPDATE", response => RaiseParsed("ONLINE_UPDATE", response, OnlineUpdated));
        socket.On("PLAYER_PARTY_INVITES_STATE", response => RaiseParsed("PLAYER_PARTY_INVITES_STATE", response, PartyInvitationsUpdated));
        socket.On("PARTY_INVITE_RECEIVED", response => RaiseParsed("PARTY_INVITE_RECEIVED", response, PartyInviteReceived));
        socket.On("PARTY_INVITE_EXPIRED", response => RaiseParsed("PARTY_INVITE_EXPIRED", response, PartyInviteExpired));
        socket.On("NOTIFICATION_CREATED", response => RaiseParsed("NOTIFICATION_CREATED", response, NotificationCreated));
        socket.On("GO_QUEUE", response => RaiseParsed("GO_QUEUE", response, PleaseEnterQueue));

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
        EmitAsync("ENTER_QUEUE", new EnterQueueMessage(modes), cancellationToken);

    public Task LeaveAllQueuesAsync(CancellationToken cancellationToken = default) =>
        EmitAsync("LEAVE_ALL_QUEUES", null, cancellationToken);

    public Task SetReadyCheckAsync(string roomId, bool accept, CancellationToken cancellationToken = default) =>
        EmitAsync("SET_READY_CHECK", new SetReadyCheckMessage(roomId, accept), cancellationToken);

    public Task InviteToPartyAsync(string invitedPlayerId, CancellationToken cancellationToken = default) =>
        EmitAsync("INVITE_TO_PARTY", new InviteToPartyMessage(invitedPlayerId), cancellationToken);

    public Task AcceptPartyInviteAsync(string inviteId, bool accept, CancellationToken cancellationToken = default) =>
        EmitAsync("ACCEPT_PARTY_INVITE", new AcceptPartyInviteMessage(inviteId, accept), cancellationToken);

    public Task LeavePartyAsync(CancellationToken cancellationToken = default) =>
        EmitAsync("LEAVE_PARTY", null, cancellationToken);

    private async Task EmitAsync(string eventName, object? payload, CancellationToken cancellationToken)
    {
        if (_socket == null || !_socket.Connected)
            return;

        if (payload == null)
            await _socket.EmitAsync(eventName).ConfigureAwait(false);
        else
            await _socket.EmitAsync(eventName, payload).ConfigureAwait(false);
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
        _socket?.Dispose();
        _socket = null;
    }

    private static void RaiseParsed<T>(string eventName, SocketIOResponse response, Action<T>? handler)
    {
        AppLog.Info($"Socket event received: {eventName}");
        if (handler == null)
        {
            AppLog.Info($"Socket event ignored (no handler): {eventName}");
            return;
        }

        if (TryGetPayload(response, out T? payload) && payload != null)
        {
            AppLog.Info($"Socket event parsed: {eventName}");
            handler(payload);
        }
        else
        {
            AppLog.Error($"Socket event parse failed: {eventName}");
        }
    }

    private static bool TryGetPayload<T>(SocketIOResponse response, out T? payload)
    {
        try
        {
            payload = response.GetValue<T>();
            return true;
        }
        catch
        {
            try
            {
                payload = response.GetValue<T>(0);
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Error($"Socket event parse failed", ex);
                payload = default;
                return false;
            }
        }
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
