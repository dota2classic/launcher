using System.Collections.Generic;
using System.Threading.Tasks;
using d2c_launcher.Api;
using d2c_launcher.Services;
using d2c_launcher.Tests.Fakes;
using Xunit;

namespace d2c_launcher.Tests;

public sealed class QueueSocketServiceTests
{
    private static (QueueSocketService service, FakeSocketFactory factory) Build()
    {
        var factory = new FakeSocketFactory();
        var service = new QueueSocketService(factory);
        return (service, factory);
    }

    // ── ConnectAsync guards ───────────────────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_empty_token_does_nothing()
    {
        var (svc, f) = Build();
        await svc.ConnectAsync("");
        Assert.False(f.Socket.Connected);
        Assert.Equal(GameCoordinatorState.Disconnected, svc.State);
    }

    [Fact]
    public async Task ConnectAsync_whitespace_token_does_nothing()
    {
        var (svc, f) = Build();
        await svc.ConnectAsync("   ");
        Assert.False(f.Socket.Connected);
    }

    [Fact]
    public async Task ConnectAsync_same_token_while_connected_skips_reconnect()
    {
        var (svc, f) = Build();
        await svc.ConnectAsync("token");

        var reconnects = 0;
        f.Socket.OnConnected += (_, _) => reconnects++;

        await svc.ConnectAsync("token");

        Assert.Equal(0, reconnects);
    }

    [Fact]
    public async Task ConnectAsync_different_token_while_connected_reconnects()
    {
        var factory = new FakeSocketFactory();
        var svc = new QueueSocketService(factory);

        await svc.ConnectAsync("token-a");
        Assert.True(factory.Socket.Connected);

        // New token: service should disconnect old socket and connect fresh
        await svc.ConnectAsync("token-b");
        // After reconnect the new socket is connected
        Assert.True(factory.Socket.Connected);
    }

    // ── ReconnectAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task ReconnectAsync_after_natural_drop_reconnects()
    {
        var (svc, f) = Build();
        await svc.ConnectAsync("token");
        f.Socket.SimulateDisconnect(); // network drop — token still held

        await svc.ReconnectAsync();

        Assert.True(f.Socket.Connected);
        Assert.Equal(GameCoordinatorState.Connected, svc.State);
    }

    [Fact]
    public async Task ReconnectAsync_while_connected_disconnects_then_reconnects()
    {
        var (svc, f) = Build();
        await svc.ConnectAsync("token");

        var disconnects = 0;
        f.Socket.OnDisconnected += (_, _) => disconnects++;

        await svc.ReconnectAsync();

        Assert.Equal(1, disconnects);
        Assert.True(f.Socket.Connected);
    }

    [Fact]
    public async Task ReconnectAsync_without_prior_connect_is_no_op()
    {
        var (svc, f) = Build();

        await svc.ReconnectAsync(); // never connected, no token stored

        Assert.False(f.Socket.Connected);
        Assert.Equal(GameCoordinatorState.Disconnected, svc.State);
    }

    // ── Emit guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Emit_while_not_connected_is_silent_no_op()
    {
        var (svc, f) = Build();
        // Never connected — socket is null internally
        await svc.EnterQueueAsync([MatchmakingMode._0]);
        Assert.Empty(f.Socket.EmittedEvents);
    }

    [Fact]
    public async Task Emit_after_disconnect_is_silent_no_op()
    {
        var (svc, f) = Build();
        await svc.ConnectAsync("token");
        await svc.DisconnectAsync();

        await svc.EnterQueueAsync([MatchmakingMode._0]);
        Assert.Empty(f.Socket.EmittedEvents);
    }

    // ── UpdateState dedup ─────────────────────────────────────────────────────

    [Fact]
    public async Task StateChanged_not_raised_when_state_unchanged()
    {
        var (svc, f) = Build();
        await svc.ConnectAsync("token"); // → Connected

        var extraChanges = new List<GameCoordinatorState>();
        svc.StateChanged += s => extraChanges.Add(s);

        // Fire CONNECTION_COMPLETE twice — second should be a no-op
        f.Socket.FireEvent("CONNECTION_COMPLETE");
        f.Socket.FireEvent("CONNECTION_COMPLETE");

        Assert.Single(extraChanges); // only the first fires StateChanged
    }
    [Fact]
    public async Task PlayerDeclineGame_forwards_decline_reason_event()
    {
        var (svc, f) = Build();
        await svc.ConnectAsync("token");

        PlayerDeclineGameMessage? received = null;
        svc.PlayerDeclineGame += msg => received = msg;

        var payload = new PlayerDeclineGameMessage(MatchmakingMode._0, DeclineReason.TIMEOUT);
        f.Socket.FireEvent("PLAYER_DECLINE_GAME", payload);

        Assert.Equal(payload, received);
    }
}
