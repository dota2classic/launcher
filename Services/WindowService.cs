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
        // Track hide/show transitions driven by the window itself (e.g. close-to-tray Hide()).
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != "IsVisible") return;
            var nowVisible = e.NewValue is true;
            if (nowVisible && !IsWindowVisible)
            {
                IsWindowVisible = true;
                WindowShown?.Invoke();
            }
            else if (!nowVisible)
            {
                IsWindowVisible = false;
            }
        };
    }

    public void ShowAndActivate()
    {
        _window?.ShowAndActivate();
        // IsWindowVisible + WindowShown are raised via IsVisibleChanged above.
    }
}
