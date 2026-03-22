using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Util;

namespace d2c_launcher.Integration;

/// <summary>
/// Connects to the Source engine's built-in network console (<c>-netconport</c>).
/// The engine streams all console output as plain newline-delimited text and accepts
/// commands the same way.
/// </summary>
public sealed class NetConClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _tcp;
    private CancellationTokenSource? _cts;

    /// <summary>Fired on the thread-pool for each line received from the engine.</summary>
    public event Action<string>? LineReceived;

    /// <summary>Fired when the connection is lost or closed.</summary>
    public event Action? Disconnected;

    public bool IsConnected => _tcp is { Connected: true };

    public NetConClient(string host = "127.0.0.1", int port = 27005)
    {
        _host = host;
        _port = port;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(_host, _port, _cts.Token);
        AppLog.Info($"NetConClient: connected to {_host}:{_port}");
        _ = ReadLoopAsync(_cts.Token);
    }

    public async Task SendCommandAsync(string command)
    {
        if (_tcp is not { Connected: true }) return;
        var stream = _tcp.GetStream();
        var writer = new StreamWriter(stream) { AutoFlush = true };
        await writer.WriteLineAsync(command);
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            var reader = new StreamReader(_tcp!.GetStream());
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break; // server closed connection
                AppLog.Info($"[NetCon] {line}");
                LineReceived?.Invoke(line);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppLog.Warn($"NetConClient: connection lost — {ex.Message}");
        }
        finally
        {
            Disconnected?.Invoke();
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _tcp?.Dispose();
    }
}
