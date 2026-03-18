using System;
using d2c_launcher.Models;

namespace d2c_launcher.Integration;

public interface ISteamManager : IDisposable
{
    User? CurrentUser { get; }
    SteamStatus SteamStatus { get; }
    string? CurrentAuthTicket { get; }
    int BridgeFailStreak { get; }
    string? LastBridgeStatus { get; }

    event Action<User?>? OnUserUpdated;
    event Action<SteamStatus>? OnSteamStatusUpdated;
    event Action<string?>? OnSteamAuthorizationChanged;
    event Action? OnSteamPolled;

    void PollSteamState();

    /// <summary>
    /// Resets the bridge failure streak counter, allowing the monitor loop to
    /// immediately retry with no backoff and re-evaluate Steam status.
    /// Called when the user clicks "Try Again" on the Steam connection screen.
    /// </summary>
    void ResetBridgeFailStreak();
}
