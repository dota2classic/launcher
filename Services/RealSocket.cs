using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using d2c_launcher.Util;
using SocketIOClient;
using SocketIOClient.Transport;

namespace d2c_launcher.Services;

/// <summary>
/// Production implementation of <see cref="ISocket"/> that wraps
/// <see cref="SocketIOClient.SocketIO"/> and owns the payload parsing logic.
/// </summary>
public sealed class RealSocket : ISocket
{
    private readonly SocketIOClient.SocketIO _socket;

    public bool Connected => _socket.Connected;

    public event EventHandler? OnConnected;
    public event EventHandler<string>? OnDisconnected;

    public RealSocket(string origin, string path, string token)
    {
        var options = new SocketIOOptions
        {
            Path = path,
            Transport = TransportProtocol.WebSocket,
            AutoUpgrade = true,
            ConnectionTimeout = TimeSpan.FromSeconds(10),
            Auth = new Dictionary<string, string>
            {
                ["token"] = token,
                ["clientType"] = "launcher"
            }
        };

        _socket = new SocketIOClient.SocketIO(origin, options);
        _socket.OnConnected += (s, e) => OnConnected?.Invoke(s, e);
        _socket.OnDisconnected += (s, e) => OnDisconnected?.Invoke(s, e);
    }

    public void On(string eventName, Action callback) =>
        _socket.On(eventName, _ => callback());

    public void On<T>(string eventName, Action<T> callback)
    {
        _socket.On(eventName, response =>
        {
            if (TryGetPayload(eventName, response, out T? payload) && payload != null)
                callback(payload);
        });
    }

    public void OnNullable<T>(string eventName, Action<T?> callback) where T : class
    {
        _socket.On(eventName, response =>
        {
            TryGetPayload(eventName, response, out T? payload);
            callback(payload);
        });
    }

    public Task EmitAsync(string eventName) => _socket.EmitAsync(eventName);
    public Task EmitAsync(string eventName, object payload) => _socket.EmitAsync(eventName, payload);
    public Task ConnectAsync() => _socket.ConnectAsync();
    public Task DisconnectAsync() => _socket.DisconnectAsync();

    public void Dispose() => _socket.Dispose();

    private static bool TryGetPayload<T>(string eventName, SocketIOResponse response, out T? payload)
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
                AppLog.Error($"Socket event parse failed: {eventName} | raw={response} | type={typeof(T).Name} | {ex}");
                payload = default;
                return false;
            }
        }
    }
}
