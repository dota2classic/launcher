using d2c_launcher.Models;

namespace d2c_launcher.Views;

/// <summary>
/// Decides whether a window-close event should be intercepted and the window hidden to the
/// system tray instead of performing a real shutdown.
/// </summary>
public static class TrayClosePolicy
{
    /// <summary>
    /// Returns <c>true</c> when the close should be suppressed and the window hidden to tray.
    /// </summary>
    /// <param name="realExit">The RealExit flag — set by the tray "Выход" action.</param>
    /// <param name="closeToTray">Whether the user has "close to tray" enabled in settings.</param>
    /// <param name="isUserClose">True when the close was triggered by the user pressing X
    ///   (<c>WindowCloseReason.WindowClosing</c>); false for programmatic or OS-initiated closes.</param>
    /// <param name="appState">Current application state.</param>
    public static bool ShouldHideToTray(bool realExit, bool closeToTray, bool isUserClose, AppState appState)
    {
        return !realExit
            && closeToTray
            && isUserClose
            && appState == AppState.Launcher;
    }
}
