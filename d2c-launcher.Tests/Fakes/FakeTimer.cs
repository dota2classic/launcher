using System;
using d2c_launcher.Util;

namespace d2c_launcher.Tests.Fakes;

/// <summary>Manually-controlled timer for tests — does not tick on its own.</summary>
public sealed class FakeTimer : IUiTimer
{
    public TimeSpan Interval { get; set; }
    public bool IsRunning { get; private set; }

    public event EventHandler? Tick;

    public void Start() => IsRunning = true;
    public void Stop()  => IsRunning = false;

    /// <summary>Fires the Tick event if the timer is running.</summary>
    public void Fire()
    {
        if (IsRunning)
            Tick?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Fires the Tick event unconditionally (ignores IsRunning).</summary>
    public void FireNow() => Tick?.Invoke(this, EventArgs.Empty);
}

/// <summary>Records all timers created so tests can fire them on demand.</summary>
public sealed class FakeTimerFactory : ITimerFactory
{
    private readonly List<FakeTimer> _created = [];

    public IReadOnlyList<FakeTimer> Created => _created;

    /// <summary>The first timer created — the countdown timer in TriviaViewModel.</summary>
    public FakeTimer Countdown => _created[0];

    /// <summary>The most recently created timer.</summary>
    public FakeTimer Latest => _created[^1];

    public IUiTimer Create()
    {
        var t = new FakeTimer();
        _created.Add(t);
        return t;
    }
}
