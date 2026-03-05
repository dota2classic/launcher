namespace d2c_launcher.Models;

public enum AppState
{
    /// <summary>Startup: Steam process status not yet determined, or Steam is running but the
    /// bridge hasn't finished querying the logged-in user.</summary>
    CheckingSteam,

    /// <summary>steam.exe is not running.</summary>
    SteamNotRunning,

    /// <summary>Steam is running but no user is logged in (offline mode or just started).</summary>
    SteamOffline,

    /// <summary>Steam is OK, but no valid game directory is configured.</summary>
    SelectGameDirectory,

    /// <summary>Steam is OK and a game directory is set. Verification / download / redist in progress.</summary>
    VerifyingGame,

    /// <summary>All checks passed. Main launcher UI is shown.</summary>
    Launcher,
}
