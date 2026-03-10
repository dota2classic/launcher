using Avalonia.Controls;
using Avalonia.Interactivity;
using d2c_launcher.Services;

namespace d2c_launcher.Views;

public partial class MainWindow : Window
{
    /// <summary>
    /// When true, the next Close() call performs a real shutdown instead of hiding to tray.
    /// Set by the tray "Выход" action before calling Close().
    /// </summary>
    public bool RealExit { get; set; }

    private readonly ISettingsStorage _settingsStorage;

    public MainWindow(ISettingsStorage settingsStorage)
    {
        _settingsStorage = settingsStorage;
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Only intercept when the user explicitly pressed the X button.
        // All other reasons (OS shutdown, programmatic Close() for updates/exit) pass through.
        if (!RealExit && _settingsStorage.Get().CloseToTray && e.CloseReason == WindowCloseReason.WindowClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }
}