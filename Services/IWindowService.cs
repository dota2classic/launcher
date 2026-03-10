using System;

namespace d2c_launcher.Services;

/// <summary>
/// Allows services and ViewModels to bring the main window to the foreground,
/// e.g. when a match is found while the launcher is minimized to tray.
/// </summary>
public interface IWindowService
{
    void ShowAndActivate();

    /// <summary>True while the main window is visible to the user.</summary>
    bool IsWindowVisible { get; }

    /// <summary>Raised on the UI thread whenever the window becomes visible.</summary>
    event Action? WindowShown;
}
