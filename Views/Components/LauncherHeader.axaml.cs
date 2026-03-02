using Avalonia.Controls;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Views.Components;

public partial class LauncherHeader : UserControl
{
    public LauncherHeader()
    {
        InitializeComponent();
    }

    private void OnPlayClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainLauncherViewModel vm)
            return;

        if (vm.Launch.PlayButtonIsStop)
            vm.Launch.StopGame();
        else
            vm.LaunchGame();
    }

    private void OnSettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.OpenSettings();
    }
}
