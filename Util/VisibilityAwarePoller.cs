using System;
using System.Threading.Tasks;
using d2c_launcher.Services;

namespace d2c_launcher.Util;

public sealed record PollSchedule(TimeSpan VisibleInterval, TimeSpan? HiddenInterval = null);

public sealed class VisibilityAwarePoller : IDisposable
{
    private readonly IUiTimer _timer;
    private readonly IWindowService _window;
    private readonly PollSchedule _schedule;
    private readonly Func<Task> _poll;
    private readonly Func<bool> _isActive;
    private readonly string _name;
    private TimeSpan? _currentInterval;
    private bool _started;
    private bool _inFlight;

    public VisibilityAwarePoller(
        ITimerFactory timerFactory,
        IWindowService window,
        string name,
        PollSchedule schedule,
        Func<Task> poll,
        Func<bool>? isActive = null)
    {
        _window = window;
        _name = name;
        _schedule = schedule;
        _poll = poll;
        _isActive = isActive ?? (() => true);
        _timer = timerFactory.Create();
        _timer.Tick += OnTick;
        _window.WindowOnScreenChanged += Evaluate;
    }

    public void Start(bool pollIfVisible = false)
    {
        if (_started)
            return;
        _started = true;
        var interval = ResolveInterval();
        Apply(interval);
        if (pollIfVisible && interval == _schedule.VisibleInterval)
            PollNow();
    }

    public void Evaluate()
    {
        if (!_started)
            return;

        var previous = _currentInterval;
        var next = ResolveInterval();
        Apply(next);
        if (next == _schedule.VisibleInterval && previous != next)
            PollNow();
    }

    public void PollNow()
    {
        if (_inFlight)
            return;
        RunAsync().FireAndForget(_name);
    }

    private TimeSpan? ResolveInterval()
    {
        if (!_isActive())
            return null;
        return _window.IsWindowOnScreen ? _schedule.VisibleInterval : _schedule.HiddenInterval;
    }

    private void Apply(TimeSpan? interval)
    {
        if (interval == _currentInterval)
            return;

        _currentInterval = interval;
        _timer.Stop();
        if (interval is not { } value)
            return;

        _timer.Interval = value;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e) => PollNow();

    private async Task RunAsync()
    {
        _inFlight = true;
        try { await _poll(); }
        finally { _inFlight = false; }
    }

    public void Dispose()
    {
        _window.WindowOnScreenChanged -= Evaluate;
        _timer.Tick -= OnTick;
        _timer.Stop();
    }
}
