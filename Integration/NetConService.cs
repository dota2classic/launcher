using System;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Util;

namespace d2c_launcher.Integration;

/// <summary>
/// Singleton service that manages a persistent NetCon connection to the running
/// Dota instance. Retries on connect failure and disconnects cleanly on game exit.
/// </summary>
public sealed class NetConService : INetConService
{
    private const int MaxAttempts = 24;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private NetConClient? _client;
    private CancellationTokenSource? _connectCts;
    private bool _isConnected;

    public event Action<string>? LineReceived;

    public bool IsConnected => _isConnected;

    public async Task StartConnectAsync(CancellationToken ct = default)
    {
        Disconnect();

        _connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _connectCts.Token;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (token.IsCancellationRequested) return;

            var client = new NetConClient();
            client.LineReceived += line => LineReceived?.Invoke(line);
            client.Disconnected += OnClientDisconnected;

            try
            {
                await client.ConnectAsync(token);
                _client = client;
                _isConnected = true;
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
                AppLog.Info($"NetConService: attempt {attempt}/{MaxAttempts} failed — {ex.Message}");
                try { await Task.Delay(RetryDelay, token); }
                catch (OperationCanceledException) { return; }
            }
        }

        AppLog.Warn("NetConService: gave up connecting after all attempts");
    }

    private void OnClientDisconnected()
    {
        _isConnected = false;
        AppLog.Info("NetConService: connection lost");
    }

    public void Disconnect()
    {
        _connectCts?.Cancel();
        _connectCts?.Dispose();
        _connectCts = null;

        _client?.Dispose();
        _client = null;
        _isConnected = false;
    }

    public async Task SendCommandAsync(string command)
    {
        if (_client == null) return;
        await _client.SendCommandAsync(command);
    }

    public void Dispose() => Disconnect();
}
