using d2c_launcher.ViewModels;
using Xunit;

namespace d2c_launcher.Tests;

public class ServerUrlTrackerTests
{
    // ── null / empty inputs ───────────────────────────────────────────────────

    [Fact]
    public void NullUrl_DoesNotConnect()
    {
        var tracker = new ServerUrlTracker();
        Assert.False(tracker.ShouldConnect(null));
    }

    [Fact]
    public void EmptyUrl_DoesNotConnect()
    {
        var tracker = new ServerUrlTracker();
        Assert.False(tracker.ShouldConnect(""));
    }

    // ── first URL ────────────────────────────────────────────────────────────

    [Fact]
    public void FirstUrl_ShouldConnect()
    {
        var tracker = new ServerUrlTracker();
        Assert.True(tracker.ShouldConnect("1.2.3.4:27015"));
    }

    // ── same URL repeated ────────────────────────────────────────────────────

    [Fact]
    public void SameUrl_SecondTime_DoesNotConnect()
    {
        var tracker = new ServerUrlTracker();
        tracker.ShouldConnect("1.2.3.4:27015");
        Assert.False(tracker.ShouldConnect("1.2.3.4:27015"));
    }

    [Fact]
    public void SameUrl_ManyTimes_DoesNotConnect()
    {
        var tracker = new ServerUrlTracker();
        tracker.ShouldConnect("1.2.3.4:27015");
        for (var i = 0; i < 5; i++)
            Assert.False(tracker.ShouldConnect("1.2.3.4:27015"));
    }

    // ── URL cleared then same URL returns ────────────────────────────────────

    [Fact]
    public void UrlCleared_ThenSameUrlReturns_ShouldConnect()
    {
        var tracker = new ServerUrlTracker();
        tracker.ShouldConnect("1.2.3.4:27015");
        tracker.ShouldConnect(null);                         // game ended
        Assert.True(tracker.ShouldConnect("1.2.3.4:27015")); // new match on same server
    }

    [Fact]
    public void EmptyStringClears_ThenSameUrlReturns_ShouldConnect()
    {
        var tracker = new ServerUrlTracker();
        tracker.ShouldConnect("1.2.3.4:27015");
        tracker.ShouldConnect("");
        Assert.True(tracker.ShouldConnect("1.2.3.4:27015"));
    }

    // ── different URL (new match) ─────────────────────────────────────────────

    [Fact]
    public void DifferentUrl_ShouldConnect()
    {
        var tracker = new ServerUrlTracker();
        tracker.ShouldConnect("1.2.3.4:27015");
        Assert.True(tracker.ShouldConnect("5.6.7.8:27015"));
    }
}
