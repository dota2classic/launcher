using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;

namespace d2c_launcher.ViewModels;

/// <summary>Base class for all toast/notification items shown in the notification area.</summary>
public abstract partial class NotificationViewModel : ViewModelBase
{
    private readonly DispatcherTimer _timer;

    public RelayCommand DismissCommand { get; }

    /// <summary>Fired on the UI thread when this notification should be removed.</summary>
    public event Action<NotificationViewModel>? Closed;

    public int DisplaySeconds { get; }

    protected NotificationViewModel(int displaySeconds)
    {
        DisplaySeconds = displaySeconds;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(displaySeconds) };
        _timer.Tick += (_, _) => ForceClose();
        _timer.Start();

        DismissCommand = new RelayCommand(ForceClose);
    }

    /// <summary>Removes the notification immediately (timer expired or user dismissed).</summary>
    public void ForceClose()
    {
        _timer.Stop();
        Dispatcher.UIThread.Post(() => Closed?.Invoke(this));
    }

    protected void StopTimer() => _timer.Stop();
}
