using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace d2c_launcher.ViewModels;

public partial class PartyInviteNotificationViewModel : ViewModelBase
{
    private const int TimeoutSeconds = 60;

    private readonly string _inviteId;
    private readonly Func<string, bool, Task> _respond;
    private readonly DispatcherTimer _timer;

    public string InviteId => _inviteId;
    public string InviterName { get; }

    [ObservableProperty]
    private Bitmap? _inviterAvatarImage;

    [ObservableProperty]
    private int _remainingSeconds = TimeoutSeconds;

    public IAsyncRelayCommand AcceptCommand { get; }
    public IAsyncRelayCommand DeclineCommand { get; }

    /// <summary>Fired on the UI thread when this notification should be removed.</summary>
    public event Action<PartyInviteNotificationViewModel>? Closed;

    public PartyInviteNotificationViewModel(string inviteId, string inviterName, Func<string, bool, Task> respond)
    {
        _inviteId = inviteId;
        InviterName = inviterName;
        _respond = respond;

        AcceptCommand = new AsyncRelayCommand(() => RespondAsync(true));
        DeclineCommand = new AsyncRelayCommand(() => RespondAsync(false));

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void LoadAvatar(Func<Task<Bitmap?>> loader)
    {
        _ = Task.Run(async () =>
        {
            var bitmap = await loader().ConfigureAwait(false);
            Dispatcher.UIThread.Post(() => InviterAvatarImage = bitmap);
        });
    }

    /// <summary>Called externally when the server says the invite expired.</summary>
    public void ForceClose()
    {
        _timer.Stop();
        Closed?.Invoke(this);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        RemainingSeconds--;
        if (RemainingSeconds <= 0)
        {
            _timer.Stop();
            Closed?.Invoke(this);
        }
    }

    private async Task RespondAsync(bool accept)
    {
        _timer.Stop();
        try { await _respond(_inviteId, accept).ConfigureAwait(false); } catch { }
        Dispatcher.UIThread.Post(() => Closed?.Invoke(this));
    }
}
