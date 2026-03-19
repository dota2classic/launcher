using System;
using d2c_launcher.Integration;
using d2c_launcher.Models;

namespace d2c_launcher.Tests.Fakes;

/// <summary>
/// In-memory ISteamManager that lets tests drive Steam state directly
/// without spawning a real SteamBridge process.
/// </summary>
public sealed class FakeSteamManager : ISteamManager
{
    public User? CurrentUser { get; set; }
    public SteamStatus SteamStatus { get; set; } = SteamStatus.Checking;
    public string? CurrentAuthTicket { get; set; }
    public int BridgeFailStreak { get; set; }
    public string? LastBridgeStatus { get; set; }

    public event Action<User?>? OnUserUpdated;
    public event Action<SteamStatus>? OnSteamStatusUpdated;
    public event Action<string?>? OnSteamAuthorizationChanged;
    public event Action? OnSteamPolled;

    public void PollSteamState() { }

    public void ResetBridgeFailStreak() => BridgeFailStreak = 0;

    public void Dispose() { }

    // ── Test helpers ──────────────────────────────────────────────────────────

    /// <summary>Updates SteamStatus and fires OnSteamStatusUpdated.</summary>
    public void SimulateStatusUpdate(SteamStatus status)
    {
        SteamStatus = status;
        OnSteamStatusUpdated?.Invoke(status);
    }

    /// <summary>Updates CurrentUser and fires OnUserUpdated.</summary>
    public void SimulateUserUpdated(User? user)
    {
        CurrentUser = user;
        OnUserUpdated?.Invoke(user);
    }

    /// <summary>Updates CurrentAuthTicket and fires OnSteamAuthorizationChanged.</summary>
    public void SimulateAuthTicket(string? ticket)
    {
        CurrentAuthTicket = ticket;
        OnSteamAuthorizationChanged?.Invoke(ticket);
    }

    /// <summary>Fires OnSteamPolled (used by the "waiting for Steam" screen).</summary>
    public void SimulatePoll() => OnSteamPolled?.Invoke();
}
