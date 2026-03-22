using System;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Util;

namespace d2c_launcher.Integration;

/// <summary>
/// Singleton service that manages a persistent NetCon connection to the running
/// Dota instance. Retries on connect failure and disconnects cleanly on game exit.
/// Retry window: up to 24 attempts × 5 s = ~2 minutes.
/// </summary>
public sealed class NetConService : INetConService
{
    private const int MaxAttempts = 24;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private volatile NetConClient? _client;
    private CancellationTokenSource? _connectCts;
    private volatile bool _isConnected;
    private TaskCompletionSource _connectedTcs = new();
    private readonly object _tcsLock = new();

    public event Action<string>? LineReceived;

    public bool IsConnected => _isConnected;

    public Task WaitConnectedAsync(CancellationToken ct = default)
        => _connectedTcs.Task.WaitAsync(ct);

    public async Task StartConnectAsync(CancellationToken ct = default)
    {
        Disconnect();

        _connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _connectCts.Token;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (token.IsCancellationRequested) return;

            var client = new NetConClient();
            try
            {
                await client.ConnectAsync(token);

                // Subscribe events only after a successful connect
                client.LineReceived += OnClientLineReceived;
                client.Disconnected += OnClientDisconnected;

                _client = client;
                _isConnected = true;
                _connectedTcs.TrySetResult();
                AppLog.Info($"NetConService: connected (attempt {attempt})");
                return;
            }
            catch (OperationCanceledException)
            {
                client.Dispose();
                return;
            }
            catch (Exception ex)
            {
                client.Dispose();
                AppLog.Warn($"NetConService: attempt {attempt}/{MaxAttempts} failed — {ex.Message}");
                try { await Task.Delay(RetryDelay, token); }
                catch (OperationCanceledException) { return; }
            }
        }

        AppLog.Warn("NetConService: gave up connecting after all attempts");
    }

    private void OnClientLineReceived(string line) => LineReceived?.Invoke(line);

    private void OnClientDisconnected()
    {
        _isConnected = false;
        ResetTcs();
        AppLog.Info("NetConService: connection lost");
    }

    private void ResetTcs()
    {
        TaskCompletionSource old;
        lock (_tcsLock)
        {
            old = _connectedTcs;
            _connectedTcs = new TaskCompletionSource();
        }
        old.TrySetCanceled();
    }

    public void Disconnect()
    {
        _connectCts?.Cancel();
        _connectCts?.Dispose();
        _connectCts = null;

        var client = _client;
        if (client != null)
        {
            client.LineReceived -= OnClientLineReceived;
            client.Disconnected -= OnClientDisconnected;
            client.Dispose();
            _client = null;
        }
        _isConnected = false;

        // Reset the TCS so future WaitConnectedAsync calls wait for the next connect
        ResetTcs();
    }

    public async Task SendCommandAsync(string command)
    {
        var client = _client; // capture to avoid TOCTOU if Disconnect races
        if (client == null) return;
        await client.SendCommandAsync(command);
    }

    public void Dispose() => Disconnect();
}
