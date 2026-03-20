using System;
using Avalonia.Threading;

namespace d2c_launcher.Util;

public sealed class AvaloniaTimerFactory : ITimerFactory
{
    public IUiTimer Create() => new AvaloniaTimerWrapper();
}

internal sealed class AvaloniaTimerWrapper : IUiTimer
{
    private readonly DispatcherTimer _timer = new();

    public TimeSpan Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    public event EventHandler Tick
    {
        add    => _timer.Tick += value;
        remove => _timer.Tick -= value;
    }

    public void Start() => _timer.Start();
    public void Stop()  => _timer.Stop();
}
