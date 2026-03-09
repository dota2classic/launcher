using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

/// <summary>
/// Enforces a single launcher instance using a named mutex.
/// The first instance runs a named pipe server; subsequent instances send their
/// command-line arguments to the first and exit.
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "d2c-launcher-single-instance";
    private const string PipeName = "d2c-launcher-ipc";

    private readonly CancellationTokenSource _cts = new();
    private Mutex? _mutex;

    /// <summary>
    /// Fired on the thread-pool when a second instance sends its args.
    /// The string is the raw argument (e.g. "d2c://spectate/123").
    /// </summary>
    public event Action<string>? OnMessageReceived;

    /// <summary>
    /// Try to become the primary instance.
    /// Returns <c>true</c> if this process is the first instance (owns the mutex).
    /// Returns <c>false</c> if another instance is already running.
    /// </summary>
    public bool TryBecomePrimaryInstance(string[] args)
    {
        _mutex = new Mutex(initiallyOwned: true, name: MutexName, out var createdNew);
        if (createdNew)
            return true;

        // Another instance is running — forward our args to it.
        ForwardArgsToPrimary(args);
        return false;
    }

    /// <summary>
    /// Start the named pipe server. Call this after <see cref="TryBecomePrimaryInstance"/> returns true.
    /// </summary>
    public void StartPipeServer()
    {
        _ = Task.Run(() => PipeServerLoopAsync(_cts.Token));
    }

    private async Task PipeServerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                using var reader = new StreamReader(server);
                var message = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(message))
                {
                    AppLog.Info($"[SingleInstance] received message: {message}");
                    OnMessageReceived?.Invoke(message.Trim());
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLog.Error("[SingleInstance] pipe server error", ex);
                // Brief pause to avoid tight error loop
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }
    }

    private static void ForwardArgsToPrimary(string[] args)
    {
        var payload = string.Join("\n", args);
        if (string.IsNullOrWhiteSpace(payload))
            return;

        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(timeout: 2000);
            using var writer = new StreamWriter(client);
            writer.Write(payload);
            writer.Flush();
        }
        catch (Exception ex)
        {
            AppLog.Error("[SingleInstance] failed to forward args to primary instance", ex);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
    }
}
