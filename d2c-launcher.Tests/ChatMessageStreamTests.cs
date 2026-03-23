using System.Runtime.CompilerServices;
using d2c_launcher.Models;
using Xunit;
using d2c_launcher.Services;
using NSubstitute;

namespace d2c_launcher.Tests;

public sealed class ChatMessageStreamTests
{
    private static ChatMessageData Msg(string id) =>
        new(id, "t", "hello", "2025-01-01T00:00:00Z", "s1", "Alice", null, false);

    private static async IAsyncEnumerable<ChatMessageData> YieldMessages(
        IEnumerable<ChatMessageData> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var msg in messages)
        {
            ct.ThrowIfCancellationRequested();
            yield return msg;
        }
    }

    private static async IAsyncEnumerable<ChatMessageData> HangForever(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        yield break;
    }

    // ── Messages are forwarded to the event ───────────────────────────────────

    [Fact]
    public async Task Start_StreamYieldsMessages_MessageReceivedFiredForEach()
    {
        var msgs = new[] { Msg("1"), Msg("2"), Msg("3") };
        var api = Substitute.For<IBackendApiService>();
        api.SubscribeChatAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(callInfo => YieldMessages(msgs, callInfo.ArgAt<CancellationToken>(1)));

        var stream = new ChatMessageStream("t", api);
        var received = new List<ChatMessageData>();
        var allDone = new TaskCompletionSource();
        stream.MessageReceived += msg =>
        {
            received.Add(msg);
            if (received.Count == msgs.Length) allDone.TrySetResult();
        };

        stream.Start();
        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(3, received.Count);
        Assert.Equal("1", received[0].MessageId);
        Assert.Equal("2", received[1].MessageId);
        Assert.Equal("3", received[2].MessageId);

        stream.Dispose();
    }

    // ── Start() is idempotent — double-call does not restart the loop ─────────

    [Fact]
    public async Task Start_CalledTwice_SecondCallIsNoOp()
    {
        var api = Substitute.For<IBackendApiService>();
        api.SubscribeChatAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(callInfo => HangForever(callInfo.ArgAt<CancellationToken>(1)));

        var stream = new ChatMessageStream("t", api);
        stream.Start();
        stream.Start(); // should be a no-op

        await Task.Delay(50); // let the loop(s) start

        api.Received(1).SubscribeChatAsync("t", Arg.Any<CancellationToken>());

        stream.Dispose();
    }

    // ── Restart() cancels the current loop and begins a new one ──────────────

    [Fact]
    public async Task Restart_CancelsOldLoopAndStartsNewOne()
    {
        var callCount = 0;
        var secondStarted = new TaskCompletionSource();

        var api = Substitute.For<IBackendApiService>();
        api.SubscribeChatAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(callInfo =>
           {
               var n = Interlocked.Increment(ref callCount);
               if (n == 2) secondStarted.TrySetResult();
               return HangForever(callInfo.ArgAt<CancellationToken>(1));
           });

        var stream = new ChatMessageStream("t", api);
        stream.Start();
        await Task.Delay(20); // let first loop get into SubscribeChatAsync

        stream.Restart();
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, callCount);

        stream.Dispose();
    }

    // ── Dispose stops the loop — no MessageReceived after disposal ────────────

    [Fact]
    public async Task Dispose_StopsLoop_NoMessagesRaisedAfterDispose()
    {
        var api = Substitute.For<IBackendApiService>();
        api.SubscribeChatAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(callInfo => HangForever(callInfo.ArgAt<CancellationToken>(1)));

        var stream = new ChatMessageStream("t", api);
        var received = 0;
        stream.MessageReceived += _ => received++;

        stream.Start();
        await Task.Delay(20); // let loop settle

        stream.Dispose();
        await Task.Delay(20); // ensure any in-flight continuations finish

        Assert.Equal(0, received);
    }
}
