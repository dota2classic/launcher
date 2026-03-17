using System;
using Avalonia.Threading;

namespace d2c_launcher.Services;

/// <summary>Production implementation — posts to the Avalonia UI thread.</summary>
public sealed class AvaloniaDispatcher : IUiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
