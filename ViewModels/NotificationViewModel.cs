using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace d2c_launcher.ViewModels;

/// <summary>Base class for all toast/notification items shown in the notification area.</summary>
public abstract partial class NotificationViewModel : ViewModelBase
{
    private const int CloseAnimationMs = 220;

    private readonly DispatcherTimer _timer;

    public RelayCommand DismissCommand { get; }

    /// <summary>Fired on the UI thread when this notification should be removed.</summary>
    public event Action<NotificationViewModel>? Closed;

    public int DisplaySeconds { get; }

    /// <summary>True while the exit animation is playing; drives the XAML closing style.</summary>
    [ObservableProperty]
    private bool _isClosing;

    protected NotificationViewModel(int displaySeconds)
    {
        DisplaySeconds = displaySeconds;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(displaySeconds) };
        _timer.Tick += (_, _) => ForceClose();
        _timer.Start();

        DismissCommand = new RelayCommand(ForceClose);
    }

    /// <summary>Starts the exit animation then removes the notification.</summary>
    public void ForceClose()
    {
        _timer.Stop();
        Dispatcher.UIThread.Post(() =>
        {
            IsClosing = true;
            Task.Delay(CloseAnimationMs).ContinueWith(
                _ => Dispatcher.UIThread.Post(() => Closed?.Invoke(this)));
        });
    }

    protected void StopTimer() => _timer.Stop();
}
