using System;
using System.Threading.Tasks;

namespace d2c_launcher.Services;

public interface IPaidActionService
{
    void SetSubscriptionStatus(bool hasPlus);

    /// <summary>
    /// Executes <paramref name="action"/> if the user has an active Plus subscription.
    /// If not, raises <see cref="SubscriptionRequired"/> and does nothing.
    /// </summary>
    void PaidAction(Action action);

    /// <inheritdoc cref="PaidAction(Action)"/>
    Task PaidAction(Func<Task> action);

    /// <summary>Raised on the UI thread when a paid action is blocked due to no subscription.</summary>
    event Action? SubscriptionRequired;
}
