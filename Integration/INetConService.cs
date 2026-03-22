using System;
using System.Threading;
using System.Threading.Tasks;

namespace d2c_launcher.Integration;

/// <summary>
/// Manages a persistent NetCon connection to a running Source engine instance.
/// Wraps <see cref="NetConClient"/> with lifecycle management and retry logic.
/// </summary>
public interface INetConService : IDisposable
{
    /// <summary>Fired on the thread-pool for each console line received from the engine.</summary>
    event Action<string>? LineReceived;

    /// <summary>True when the TCP connection to the game's netcon port is active.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Begins connecting to the game's netcon port, retrying with backoff until
    /// the connection succeeds or the token is cancelled.
    /// Safe to call multiple times — cancels any in-progress connect attempt first.
    /// </summary>
    Task StartConnectAsync(CancellationToken ct = default);

    /// <summary>Disconnects and cancels any in-progress connect attempt.</summary>
    void Disconnect();

    /// <summary>Sends a console command to the engine. No-op if not connected.</summary>
    Task SendCommandAsync(string command);
}
