using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Integration;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Tests.Fakes;
using NSubstitute;
using Xunit;

namespace d2c_launcher.Tests;

/// <summary>
/// Integration tests that drive AuthCoordinator through FakeSteamManager events,
/// verifying that the fake correctly triggers real code paths.
/// </summary>
public sealed class SteamStateTransitionTests
{
    private readonly IBackendApiService _api = Substitute.For<IBackendApiService>();
    private readonly IQueueSocketService _socket = Substitute.For<IQueueSocketService>();
    private readonly ISettingsStorage _settings = Substitute.For<ISettingsStorage>();
    private readonly ISteamAuthApi _steamAuth = Substitute.For<ISteamAuthApi>();
    private readonly SyncDispatcher _dispatcher = new();

    public SteamStateTransitionTests()
    {
        _settings.Get().Returns(new LauncherSettings());
    }

    [Fact]
    public async Task Auth_ticket_received_sets_bearer_and_connects_socket()
    {
        var steam = new FakeSteamManager();
        var coord = new AuthCoordinator(steam, _api, _socket, _settings, _steamAuth, _dispatcher);
        coord.Start(null);

        steam.SimulateAuthTicket("fake-ticket");

        await Task.Delay(50);

        _api.Received().SetBearerToken("fake-ticket");
        await _socket.Received().ConnectAsync("fake-ticket", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Auth_ticket_cleared_disconnects_socket_and_wipes_persisted_token()
    {
        var steam = new FakeSteamManager();
        var coord = new AuthCoordinator(steam, _api, _socket, _settings, _steamAuth, _dispatcher);
        coord.Start("old-token");
        await Task.Delay(20);

        steam.SimulateAuthTicket(null);

        await Task.Delay(50);

        _api.Received().SetBearerToken(null);
        await _socket.Received().DisconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SimulatePoll_fires_OnSteamPolled()
    {
        var steam = new FakeSteamManager();
        var fired = false;
        steam.OnSteamPolled += () => fired = true;

        steam.SimulatePoll();

        Assert.True(fired);
    }

    [Fact]
    public void ResetBridgeFailStreak_resets_counter()
    {
        var steam = new FakeSteamManager { BridgeFailStreak = 5 };
        steam.ResetBridgeFailStreak();
        Assert.Equal(0, steam.BridgeFailStreak);
    }
}
