using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;

namespace d2c_launcher.ViewModels;

public partial class LaunchSteamFirstViewModel : ViewModelBase
{
    public Action? TryAgainCallback { get; set; }

    [RelayCommand]
    private void OpenSteam()
    {
        try
        {
            Process.Start(new ProcessStartInfo("steam://") { UseShellExecute = true });
        }
        catch
        {
            // If steam:// protocol is not registered, ignore
        }
    }

    [RelayCommand]
    private void TryAgain() => TryAgainCallback?.Invoke();
}
