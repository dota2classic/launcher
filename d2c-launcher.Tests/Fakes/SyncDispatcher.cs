using System;
using d2c_launcher.Services;

namespace d2c_launcher.Tests.Fakes;

/// <summary>Executes posted actions synchronously on the calling thread — no Avalonia needed.</summary>
public sealed class SyncDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}
