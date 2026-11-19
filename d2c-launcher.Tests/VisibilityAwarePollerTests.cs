using d2c_launcher.Services;
using d2c_launcher.Tests.Fakes;
using d2c_launcher.Util;
using NSubstitute;
using Xunit;

namespace d2c_launcher.Tests;

public sealed class VisibilityAwarePollerTests
{
    private static readonly TimeSpan Visible = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Hidden = TimeSpan.FromMinutes(1);

    private sealed class Harness
    {
        public readonly FakeTimerFactory Timers = new();
        public readonly IWindowService Window = Substitute.For<IWindowService>();
        public bool Active = true;
        public int Polls;
        public VisibilityAwarePoller Poller = null!;
        public FakeTimer Timer => Timers.Latest;

        public Harness(bool onScreen, TimeSpan? hidden)
        {
            Window.IsWindowOnScreen.Returns(onScreen);
            Poller = new VisibilityAwarePoller(Timers, Window, "test", new PollSchedule(Visible, hidden),
                () => { Polls++; return Task.CompletedTask; }, () => Active);
        }

        public void SetOnScreen(bool onScreen)
        {
            Window.IsWindowOnScreen.Returns(onScreen);
            Window.WindowOnScreenChanged += Raise.Event<Action>();
        }
    }

    [Fact]
    public void Visible_PollsOnEachTickAtVisibleInterval()
    {
        var h = new Harness(onScreen: true, hidden: null);
        h.Poller.Start();

        Assert.True(h.Timer.IsRunning);
        Assert.Equal(Visible, h.Timer.Interval);
        h.Timer.Fire();
        h.Timer.Fire();
        Assert.Equal(2, h.Polls);
    }

    [Fact]
    public void Hidden_WithoutHiddenInterval_PausesAndCatchesUpOnShow()
    {
        var h = new Harness(onScreen: true, hidden: null);
        h.Poller.Start();

        h.SetOnScreen(false);
        Assert.False(h.Timer.IsRunning);

        h.SetOnScreen(true);
        Assert.True(h.Timer.IsRunning);
        Assert.Equal(1, h.Polls);
    }

    [Fact]
    public void Hidden_WithHiddenInterval_SlowsDown()
    {
        var h = new Harness(onScreen: true, hidden: Hidden);
        h.Poller.Start();

        h.SetOnScreen(false);

        Assert.True(h.Timer.IsRunning);
        Assert.Equal(Hidden, h.Timer.Interval);
        Assert.Equal(0, h.Polls);
    }

    [Fact]
    public void Inactive_StopsUntilActiveAgain()
    {
        var h = new Harness(onScreen: true, hidden: Hidden);
        h.Active = false;
        h.Poller.Start(pollIfVisible: true);

        Assert.False(h.Timer.IsRunning);
        Assert.Equal(0, h.Polls);

        h.Active = true;
        h.Poller.Evaluate();
        Assert.True(h.Timer.IsRunning);
        Assert.Equal(1, h.Polls);
    }

    [Fact]
    public void Dispose_StopsTimerAndIgnoresWindowEvents()
    {
        var h = new Harness(onScreen: false, hidden: null);
        h.Poller.Start();
        h.Poller.Dispose();

        h.SetOnScreen(true);

        Assert.False(h.Timer.IsRunning);
        Assert.Equal(0, h.Polls);
    }
}
