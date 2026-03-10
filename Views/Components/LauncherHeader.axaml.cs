using Avalonia.Controls;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Views.Components;

public partial class LauncherHeader : UserControl
{
    public LauncherHeader()
    {
        InitializeComponent();
    }

    private void OnPlayTabClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.NavigateTo(LauncherTab.Play);
    }

    private void OnSettingsTabClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
        {
            vm.NavigateTo(LauncherTab.Settings);
            _ = vm.Settings.LoadDlcPackagesAsync();
        }
    }

    private void OnProfileTabClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.NavigateTo(vm.ActiveTab == LauncherTab.Profile ? LauncherTab.Play : LauncherTab.Profile);
    }
}
