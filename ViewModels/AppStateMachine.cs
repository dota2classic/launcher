using d2c_launcher.Models;

namespace d2c_launcher.ViewModels;

/// <summary>
/// Pure, stateless transition function for the top-level application state machine.
/// All methods are deterministic and have no side effects — safe to unit test without any UI or services.
/// </summary>
public static class AppStateMachine
{
    /// <summary>
    /// Computes the next <see cref="AppState"/> in response to a Steam status/user update.
    /// </summary>
    /// <param name="current">The state the app is currently in.</param>
    /// <param name="steamStatus">The latest <see cref="SteamStatus"/> reported by SteamManager.</param>
    /// <param name="hasUser">Whether SteamManager.CurrentUser is non-null.</param>
    /// <param name="hasGameDir">Whether a valid game directory is currently configured in settings.</param>
    /// <returns>The state the app should transition to.</returns>
    /// <remarks>
    /// Sticky rule: once the app reaches <see cref="AppState.VerifyingGame"/> or
    /// <see cref="AppState.Launcher"/>, incremental Steam heartbeats that don't change the
    /// essential condition (steam running + user present) do not restart the flow.
    /// Only a Steam stop/offline event will pull these states back.
    /// </remarks>
    public static AppState OnSteamUpdate(AppState current, SteamStatus steamStatus, bool hasUser, bool hasGameDir)
    {
        return steamStatus switch
        {
            SteamStatus.NotRunning => AppState.SteamNotRunning,
            SteamStatus.Offline    => AppState.SteamOffline,

            // Still waiting for the bridge to complete its first query
            SteamStatus.Checking   => AppState.CheckingSteam,

            SteamStatus.Running when !hasUser => AppState.CheckingSteam,

            SteamStatus.Running =>
                // Steam is fully ready. Apply sticky rule: don't interrupt an in-progress
                // verification or a running launcher session.
                current is AppState.VerifyingGame or AppState.Launcher
                    ? current
                    : hasGameDir
                        ? AppState.VerifyingGame
                        : AppState.SelectGameDirectory,

            _ => current
        };
    }

    /// <summary>
    /// Transition fired when the user picks a game directory on the SelectGame screen.
    /// Always moves to <see cref="AppState.VerifyingGame"/> regardless of current state.
    /// </summary>
    public static AppState OnGameDirSelected(AppState current) => AppState.VerifyingGame;

    /// <summary>
    /// Transition fired when <see cref="GameDownloadViewModel"/> completes all phases successfully.
    /// Always moves to <see cref="AppState.Launcher"/>.
    /// </summary>
    public static AppState OnVerificationCompleted(AppState current) => AppState.Launcher;

    /// <summary>
    /// Transition fired when the game directory changes from within the settings panel
    /// (while already in <see cref="AppState.Launcher"/>). Re-runs verification for the new path.
    /// Always moves to <see cref="AppState.VerifyingGame"/>.
    /// </summary>
    public static AppState OnGameDirChanged(AppState current) => AppState.VerifyingGame;
}
