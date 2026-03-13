using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

public partial class LaunchSteamFirstViewModel : ViewModelBase, IDisposable
{
    private readonly DispatcherTimer _debugTimer;

    public Action? TryAgainCallback { get; set; }
    public Func<Dictionary<string, string>>? GetDiagnostics { get; set; }
    public event Action? CheckOccurred;

    [ObservableProperty] private bool _showDebugHint;
    [ObservableProperty] private bool _debugInfoSent;

    public LaunchSteamFirstViewModel()
    {
        _debugTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _debugTimer.Tick += (_, _) =>
        {
            _debugTimer.Stop();
            ShowDebugHint = true;
        };
        _debugTimer.Start();
    }

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

    public void FireCheck() => CheckOccurred?.Invoke();

    [RelayCommand]
    private void TryAgain() => TryAgainCallback?.Invoke();

    [RelayCommand]
    private void SendDebugInfo()
    {
        var attrs = GetDiagnostics?.Invoke() ?? [];
        FaroTelemetryService.TrackEvent("steam_detection_failed", attrs);
        DebugInfoSent = true;
    }

    public void Dispose()
    {
        _debugTimer.Stop();
    }
}
