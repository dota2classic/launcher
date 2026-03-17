using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using d2c_launcher.Services;

namespace d2c_launcher.Tests.Fakes;

/// <summary>
/// In-memory ISocket that lets tests fire typed socket events directly
/// without a real WebSocket connection.
/// </summary>
public sealed class FakeSocket : ISocket
{
    public bool Connected { get; private set; }

    public event EventHandler? OnConnected;
    public event EventHandler<string>? OnDisconnected;

    private readonly Dictionary<string, Action> _noPayloadHandlers = new();

    // Wrap handlers with a delegate that accepts object? so we can store them uniformly.
    private readonly Dictionary<string, List<Action<object?>>> _typedHandlers = new();

    public void On(string eventName, Action callback) =>
        _noPayloadHandlers[eventName] = callback;

    public void On<T>(string eventName, Action<T> callback) =>
        GetTypedList(eventName).Add(payload => { if (payload is T t) callback(t); });

    public void OnNullable<T>(string eventName, Action<T?> callback) where T : class =>
        GetTypedList(eventName).Add(payload => callback(payload as T));

    public Task EmitAsync(string eventName) { EmittedEvents.Add((eventName, null)); return Task.CompletedTask; }
    public Task EmitAsync(string eventName, object payload) { EmittedEvents.Add((eventName, payload)); return Task.CompletedTask; }

    public Task ConnectAsync() { SimulateConnect(); return Task.CompletedTask; }
    public Task DisconnectAsync() { SimulateDisconnect(); return Task.CompletedTask; }
    public void Dispose() { }

    // ── Test helpers ──────────────────────────────────────────────────────────

    public List<(string Event, object? Payload)> EmittedEvents { get; } = new();

    /// <summary>Simulates the socket successfully connecting.</summary>
    public void SimulateConnect()
    {
        Connected = true;
        OnConnected?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Simulates the server disconnecting the socket.</summary>
    public void SimulateDisconnect()
    {
        Connected = false;
        OnDisconnected?.Invoke(this, "transport close");
    }

    /// <summary>Fires a no-payload event (e.g. CONNECTION_COMPLETE).</summary>
    public void FireEvent(string eventName)
    {
        if (_noPayloadHandlers.TryGetValue(eventName, out var cb))
            cb();
    }

    /// <summary>Fires a typed event, invoking all registered handlers for that name.</summary>
    public void FireEvent<T>(string eventName, T payload) => Dispatch(eventName, payload);

    /// <summary>Fires a nullable event with a null payload (e.g. "state cleared").</summary>
    public void FireNullEvent<T>(string eventName) where T : class => Dispatch(eventName, null);

    private void Dispatch(string eventName, object? payload)
    {
        if (!_typedHandlers.TryGetValue(eventName, out var handlers))
            return;
        foreach (var h in handlers)
            h(payload);
    }

    private List<Action<object?>> GetTypedList(string eventName)
    {
        if (!_typedHandlers.TryGetValue(eventName, out var list))
            _typedHandlers[eventName] = list = new List<Action<object?>>();
        return list;
    }
}
