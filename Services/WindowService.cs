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

    public bool IsWindowVisible { get; private set; } = true;
    public event Action? WindowShown;

    public void SetWindow(MainWindow window)
    {
        _window = window;
        IsWindowVisible = window.IsVisible;
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != "IsVisible") return;
            var nowVisible = e.NewValue is true;
            if (nowVisible && !IsWindowVisible)
                WindowShown?.Invoke();
            IsWindowVisible = nowVisible;
        };
    }

    public void ShowAndActivate()
    {
        // Set eagerly so socket-thread IsWindowVisible checks see it immediately,
        // preventing a duplicate Windows toast from racing with the pending Show().
        IsWindowVisible = true;
        _window?.ShowAndActivate();
        // The actual WindowShown event is still raised via IsVisibleChanged below.
    }
}
