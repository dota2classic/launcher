using System.Runtime.CompilerServices;
using d2c_launcher.Models;
using Xunit;
using d2c_launcher.Services;
using d2c_launcher.Tests.Fakes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace d2c_launcher.Tests;

public sealed class UserNameResolverTests
{
    private static PlayerLinkSegment Link(string steamId) =>
        new(steamId, $"https://example.com/players/{steamId}", steamId);

    // ── ScheduleLoads dedup ───────────────────────────────────────────────────

    [Fact]
    public async Task ScheduleLoads_SameId_CalledTwiceBeforeResolution_OnlyOneApiCall()
    {
        // Arrange
        var tcs = new TaskCompletionSource<(string?, string?)?>();
        var api = Substitute.For<IBackendApiService>();
        api.GetUserInfoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(tcs.Task);

        var resolver = new UserNameResolver(api, new SyncDispatcher());
        var segments = new RichSegment[] { Link("42") };

        // Act — two calls before the first fetch completes
        resolver.ScheduleLoads(segments);  // marks sentinel, kicks off fetch
        resolver.ScheduleLoads(segments);  // sentinel already in cache → skipped

        tcs.SetResult(("Alice", null));
        await Task.Delay(50); // allow async continuation to run

        // Assert — API called exactly once
        await api.Received(1).GetUserInfoAsync("42", Arg.Any<CancellationToken>());
    }

    // ── Null API response removes sentinel so next call retries ──────────────

    [Fact]
    public async Task ScheduleLoads_ApiReturnsNull_RemovesSentinelSoNextCallRetries()
    {
        var api = Substitute.For<IBackendApiService>();
        api.GetUserInfoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(Task.FromResult<(string?, string?)?>(null));

        var resolver = new UserNameResolver(api, new SyncDispatcher());
        var segments = new RichSegment[] { Link("99") };

        // First call → API returns null → sentinel removed
        resolver.ScheduleLoads(segments);
        await Task.Delay(50);

        Assert.False(resolver.Cache.ContainsKey("99"), "Sentinel should be removed on null response");

        // Second call → fetch fires again
        resolver.ScheduleLoads(segments);
        await Task.Delay(50);

        await api.Received(2).GetUserInfoAsync("99", Arg.Any<CancellationToken>());
    }

    // ── Successful resolution populates cache and fires NamesUpdated ──────────

    [Fact]
    public async Task ScheduleLoads_ApiReturnsName_PopulatesCacheAndFiresNamesUpdated()
    {
        var api = Substitute.For<IBackendApiService>();
        api.GetUserInfoAsync("7", Arg.Any<CancellationToken>())
           .Returns(Task.FromResult<(string?, string?)?>(("Sasha", null)));

        var resolver = new UserNameResolver(api, new SyncDispatcher());
        var updatedCount = 0;
        resolver.NamesUpdated += () => updatedCount++;

        resolver.ScheduleLoads(new RichSegment[] { Link("7") });
        await Task.Delay(50);

        Assert.Equal("Sasha", resolver.Cache["7"]);
        Assert.Equal(1, updatedCount);
    }

    // ── Null Name field falls back to steamId ─────────────────────────────────

    [Fact]
    public async Task ScheduleLoads_ApiReturnsNullName_FallsBackToSteamId()
    {
        var api = Substitute.For<IBackendApiService>();
        api.GetUserInfoAsync("55", Arg.Any<CancellationToken>())
           .Returns(Task.FromResult<(string?, string?)?>(((string?)null, (string?)null)));

        var resolver = new UserNameResolver(api, new SyncDispatcher());

        resolver.ScheduleLoads(new RichSegment[] { Link("55") });
        await Task.Delay(50);

        Assert.Equal("55", resolver.Cache["55"]);
    }

    // ── Non-PlayerLinkSegments are ignored ────────────────────────────────────

    [Fact]
    public void ScheduleLoads_NoPlayerLinkSegments_NoApiCallAndCacheEmpty()
    {
        var api = Substitute.For<IBackendApiService>();
        var resolver = new UserNameResolver(api, new SyncDispatcher());

        resolver.ScheduleLoads(new RichSegment[] { new TextSegment("hello"), new TextSegment("world") });

        api.DidNotReceiveWithAnyArgs().GetUserInfoAsync(default!, default);
        Assert.Empty(resolver.Cache);
    }
}
