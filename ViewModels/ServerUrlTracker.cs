namespace d2c_launcher.ViewModels;

/// <summary>
/// Tracks the last seen server URL and decides when to trigger auto-connect.
/// Pure logic — no Avalonia, no I/O, no async.
/// </summary>
public class ServerUrlTracker
{
    private string? _lastServerUrl;

    /// <summary>
    /// Called whenever a new <c>PlayerGameStateMessage</c> arrives.
    /// Returns <c>true</c> if auto-connect should fire (URL is new and non-empty).
    /// Resets state when <paramref name="serverUrl"/> is null/empty so the next
    /// distinct URL triggers a connect again.
    /// </summary>
    public bool ShouldConnect(string? serverUrl)
    {
        if (string.IsNullOrEmpty(serverUrl))
        {
            _lastServerUrl = null;
            return false;
        }

        if (serverUrl == _lastServerUrl)
            return false;

        _lastServerUrl = serverUrl;
        return true;
    }
}
