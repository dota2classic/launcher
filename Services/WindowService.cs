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

    public void SetWindow(MainWindow window)
    {
        _window = window;
        IsWindowVisible = window.IsVisible;
        IsWindowActive = window.IsActive;
        window.PropertyChanged += (_, e) =>
        {
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
}
