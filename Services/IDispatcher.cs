using System;

namespace d2c_launcher.Services;

/// <summary>
/// Abstracts UI-thread dispatch so that AuthCoordinator can be tested
/// without a running Avalonia application.
/// </summary>
public interface IUiDispatcher
{
    void Post(Action action);
}
