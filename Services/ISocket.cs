using System;
using System.Threading.Tasks;

namespace d2c_launcher.Services;

/// <summary>
/// Minimal socket abstraction over SocketIOClient, with typed event deserialization.
/// Allows QueueSocketService to be tested without a real WebSocket connection.
/// </summary>
public interface ISocket : IDisposable
{
    bool Connected { get; }

    event EventHandler? OnConnected;
    event EventHandler<string>? OnDisconnected;

    /// <summary>Registers a handler for an event with no payload.</summary>
    void On(string eventName, Action callback);

    /// <summary>
    /// Registers a handler for a typed event. The handler is only invoked when
    /// the payload deserializes successfully.
    /// </summary>
    void On<T>(string eventName, Action<T> callback);

    /// <summary>
    /// Registers a handler for a typed event where null is a valid value
    /// (e.g. "state cleared" semantics). The handler is invoked with null when
    /// the payload is absent or fails to deserialize.
    /// </summary>
    void OnNullable<T>(string eventName, Action<T?> callback) where T : class;

    Task EmitAsync(string eventName);
    Task EmitAsync(string eventName, object payload);
    Task ConnectAsync();
    Task DisconnectAsync();
}
