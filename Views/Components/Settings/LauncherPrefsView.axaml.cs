using Avalonia.Controls;
using Avalonia.Interactivity;

namespace d2c_launcher.Views.Components.Settings;

public partial class LauncherPrefsView : UserControl
{
    public LauncherPrefsView()
    {
        InitializeComponent();
    }

    private void OnSelectDirectoryClicked(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(SettingsPanel.SelectDirectoryRequestedEvent, this));
    }
}
