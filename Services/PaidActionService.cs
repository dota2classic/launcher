using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace d2c_launcher.Services;

public sealed class PaidActionService : IPaidActionService
{
    private volatile bool _hasSubscription;

    public event Action? SubscriptionRequired;

    public void SetSubscriptionStatus(bool hasPlus) => _hasSubscription = hasPlus;

    public void PaidAction(Action action)
    {
        if (!_hasSubscription)
        {
            Dispatcher.UIThread.Post(() => SubscriptionRequired?.Invoke());
            return;
        }
        action();
    }

    public async Task PaidAction(Func<Task> action)
    {
        if (!_hasSubscription)
        {
            Dispatcher.UIThread.Post(() => SubscriptionRequired?.Invoke());
            return;
        }
        await action();
    }
}
