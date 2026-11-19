using System;
using d2c_launcher.Views;

namespace d2c_launcher.Services;

/// <summary>
/// Concrete implementation of <see cref="IWindowService"/>.
/// The window reference is set by App after the MainWindow is created.
/// </summary>
public sealed class WindowService : IWindowService
{
    private MainWindow? _window;
    private bool _showPending;

    public bool IsWindowVisible { get; private set; } = true;
    public bool IsWindowActive { get; private set; } = true;
    public event Action? WindowShown;
    public bool IsWindowOnScreen { get; private set; } = true;
    public event Action? WindowOnScreenChanged;

    public void SetWindow(MainWindow window)
    {
        _window = window;
        IsWindowVisible = window.IsVisible;
        IsWindowActive = window.IsActive;
        IsWindowOnScreen = ComputeOnScreen(window);
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name is "IsVisible" or "WindowState")
                UpdateOnScreen(window);

            if (e.Property.Name == "IsVisible")
            {
                var nowVisible = e.NewValue is true;
                var shouldRaiseWindowShown = nowVisible && (!IsWindowVisible || _showPending);
                if (!nowVisible)
                {
                    _showPending = false;
                    IsWindowActive = false;
                }
                if (shouldRaiseWindowShown)
                    _showPending = false;
                if (shouldRaiseWindowShown)
                    WindowShown?.Invoke();
                IsWindowVisible = nowVisible;
                return;
            }

            if (e.Property.Name == "IsActive")
                IsWindowActive = e.NewValue is true;
        };
    }

    public void ShowAndActivate()
    {
        // Set eagerly so socket-thread IsWindowVisible checks see it immediately,
        // preventing a duplicate Windows toast from racing with the pending Show().
        _showPending = !IsWindowVisible;
        IsWindowVisible = true;
        IsWindowActive = true;
        _window?.ShowAndActivate();
        // The actual WindowShown event is still raised via IsVisibleChanged below.
    }

    private static bool ComputeOnScreen(MainWindow window) =>
        window.IsVisible && window.WindowState != Avalonia.Controls.WindowState.Minimized;

    private void UpdateOnScreen(MainWindow window)
    {
        var onScreen = ComputeOnScreen(window);
        if (onScreen == IsWindowOnScreen)
            return;
        IsWindowOnScreen = onScreen;
        WindowOnScreenChanged?.Invoke();
    }
}
