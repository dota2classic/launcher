using System;

namespace d2c_launcher.Util;

public interface IUiTimer
{
    TimeSpan Interval { get; set; }
    event EventHandler Tick;
    void Start();
    void Stop();
}

public interface ITimerFactory
{
    IUiTimer Create();
}
