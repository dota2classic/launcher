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

    private void OnLiveTabClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.NavigateTo(LauncherTab.Live);
    }

    private void OnStreamsTabClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.NavigateTo(LauncherTab.Streams);
    }

    private void OnSettingsTabClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.ToggleSettings();
    }

    private void OnProfileTabClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.OpenProfile();
    }

    private void OnConnectClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.Launch.ConnectToGame();
    }

    private void OnLaunchGameClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
        {
            if (vm.Launch.PlayButtonIsStop)
                vm.Launch.StopGame();
            else
                vm.Launch.LaunchGame();
        }
    }

    private void OnD2CPlusClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.OpenOwnProfileAtSubscriptionTab();
    }
}
