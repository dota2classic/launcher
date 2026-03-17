using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Integration;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Tests.Fakes;
using NSubstitute;
using Xunit;

namespace d2c_launcher.Tests;

public sealed class AuthCoordinatorTests
{
    // ── Fixture ───────────────────────────────────────────────────────────────

    private readonly ISteamManager _steam = Substitute.For<ISteamManager>();
    private readonly IBackendApiService _api = Substitute.For<IBackendApiService>();
    private readonly IQueueSocketService _socket = Substitute.For<IQueueSocketService>();
    private readonly ISettingsStorage _settings = Substitute.For<ISettingsStorage>();
    private readonly ISteamAuthApi _steamAuth = Substitute.For<ISteamAuthApi>();
    private readonly SyncDispatcher _dispatcher = new();

    private AuthCoordinator Build() => new(
        _steam, _api, _socket, _settings, _steamAuth, _dispatcher);

    public AuthCoordinatorTests()
    {
        // Default: settings returns a blank object so Get()/Save() don't throw
        _settings.Get().Returns(new LauncherSettings());
    }

    // ── Start() with persisted token ──────────────────────────────────────────

    [Fact]
    public async Task Start_with_persisted_token_sets_bearer_and_connects_socket()
    {
        _steam.CurrentAuthTicket.Returns((string?)null); // no live ticket yet

        var svc = Build();
        svc.Start("persisted-token");

        // Give the fire-and-forget task a moment to run
        await Task.Yield();

        _api.Received().SetBearerToken("persisted-token");
        await _socket.Received().ConnectAsync("persisted-token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Start_with_no_token_does_not_set_bearer()
    {
        _steam.CurrentAuthTicket.Returns((string?)null);

        var svc = Build();
        svc.Start(null);

        _api.DidNotReceive().SetBearerToken(Arg.Any<string?>());
    }

    // ── Steam auth event → full token apply flow ──────────────────────────────

    [Fact]
    public async Task Steam_token_received_sets_bearer_connects_socket_and_persists()
    {
        var svc = Build();
        svc.Start(null);

        // Simulate SteamBridge delivering a fresh auth ticket
        _steam.OnSteamAuthorizationChanged += Raise.Event<System.Action<string?>>((string?)"steam-ticket");

        // SyncDispatcher runs the posted action inline, but ApplyTokenAsync is still async
        await Task.Delay(50);

        _api.Received().SetBearerToken("steam-ticket");
        await _socket.Received().ConnectAsync("steam-ticket", Arg.Any<CancellationToken>());

        var savedSettings = _settings.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(ISettingsStorage.Save))
            .Select(c => (LauncherSettings)c.GetArguments()[0]!)
            .LastOrDefault();
        Assert.NotNull(savedSettings);
        Assert.Equal("steam-ticket", savedSettings!.BackendAccessToken);
    }

    [Fact]
    public async Task Steam_token_received_raises_TokenApplied_with_token()
    {
        var svc = Build();
        svc.Start(null);

        string? appliedToken = null;
        svc.TokenApplied += t => appliedToken = t;

        _steam.OnSteamAuthorizationChanged += Raise.Event<System.Action<string?>>((string?)"steam-ticket");
        await Task.Delay(50);

        Assert.Equal("steam-ticket", appliedToken);
    }

    // ── Null token clears everything ──────────────────────────────────────────

    [Fact]
    public async Task Null_token_clears_bearer_disconnects_socket_and_wipes_persisted_token()
    {
        var svc = Build();
        svc.Start("old-token");
        await Task.Delay(20);

        // Steam signals logout
        _steam.OnSteamAuthorizationChanged += Raise.Event<System.Action<string?>>((string?)null);
        await Task.Delay(50);

        _api.Received().SetBearerToken(null);
        await _socket.Received().DisconnectAsync(Arg.Any<CancellationToken>());

        var savedSettings = _settings.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(ISettingsStorage.Save))
            .Select(c => (LauncherSettings)c.GetArguments()[0]!)
            .LastOrDefault();
        Assert.NotNull(savedSettings);
        Assert.Null(savedSettings!.BackendAccessToken);
    }

    [Fact]
    public async Task Null_token_raises_TokenApplied_with_null()
    {
        var svc = Build();
        svc.Start(null);

        string? appliedToken = "sentinel";
        svc.TokenApplied += t => appliedToken = t;

        _steam.OnSteamAuthorizationChanged += Raise.Event<System.Action<string?>>((string?)null);
        await Task.Delay(50);

        Assert.Null(appliedToken);
    }

    // ── CurrentToken tracking ─────────────────────────────────────────────────

    [Fact]
    public async Task CurrentToken_updated_when_steam_token_arrives()
    {
        var svc = Build();
        svc.Start(null);

        _steam.OnSteamAuthorizationChanged += Raise.Event<System.Action<string?>>((string?)"new-token");
        await Task.Delay(50);

        Assert.Equal("new-token", svc.CurrentToken);
    }

    [Fact]
    public async Task CurrentToken_cleared_when_null_token_arrives()
    {
        var svc = Build();
        svc.Start("old-token");

        _steam.OnSteamAuthorizationChanged += Raise.Event<System.Action<string?>>((string?)null);
        await Task.Delay(50);

        Assert.Null(svc.CurrentToken);
    }
}
