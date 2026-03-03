using System;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

/// <summary>
/// Singleton service that owns in-memory cvar state backed by config.cfg.
/// </summary>
public interface ICvarSettingsProvider
{
    /// <summary>Current in-memory cvar state.</summary>
    CvarSettings Get();

    /// <summary>
    /// Update in-memory state and write changed cvars to config.cfg.
    /// Does NOT write if <see cref="IsGameRunning"/> is true (game owns the file).
    /// Fires <see cref="CvarChanged"/>.
    /// </summary>
    void Update(CvarSettings settings);

    /// <summary>
    /// Load cvar values from config.cfg into memory.
    /// Called on launcher startup and on game exit.
    /// Stores the game directory for subsequent <see cref="Update"/> calls.
    /// </summary>
    bool LoadFromConfigCfg(string gameDirectory);

    /// <summary>
    /// Set by GameLaunchViewModel to indicate the game is running.
    /// When true, <see cref="Update"/> skips writing to config.cfg.
    /// </summary>
    bool IsGameRunning { get; set; }

    /// <summary>Fired when any cvar value changes (from UI or from config.cfg read).</summary>
    event Action? CvarChanged;
}
