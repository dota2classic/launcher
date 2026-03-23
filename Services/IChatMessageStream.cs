using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public interface IChatMessageStream : IDisposable
{
    /// <summary>Raised (on background thread) for each message received from the SSE stream.</summary>
    event Action<ChatMessageData> MessageReceived;

    /// <summary>Starts the SSE loop. Safe to call before the auth token is set.</summary>
    void Start();

    /// <summary>Cancels the current connection and immediately reconnects. Call when the auth token changes.</summary>
    void Restart();
}

public interface IChatMessageStreamFactory
{
    IChatMessageStream Create(string threadId);
}

public sealed class ChatMessageStream : IChatMessageStream
{
    private readonly string _threadId;
    private readonly IBackendApiService _api;

    private CancellationTokenSource? _cts;

    public event Action<ChatMessageData>? MessageReceived;

    public ChatMessageStream(string threadId, IBackendApiService api)
    {
        _threadId = threadId;
        _api = api;
    }

    public void Start() => StartLoop();

    public void Restart() => StartLoop();

    private void StartLoop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await foreach (var msg in _api.SubscribeChatAsync(_threadId, ct))
                    MessageReceived?.Invoke(msg);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (ex is not HttpIOException)
                    AppLog.Error($"Chat SSE disconnected: {ex.Message}", ex);
                try { await Task.Delay(3000, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}

public sealed class ChatMessageStreamFactory : IChatMessageStreamFactory
{
    private readonly IBackendApiService _api;

    public ChatMessageStreamFactory(IBackendApiService api)
    {
        _api = api;
    }

    public IChatMessageStream Create(string threadId) => new ChatMessageStream(threadId, _api);
}
