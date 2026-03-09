using System;
using System.Collections.Generic;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

/// <summary>
/// Singleton service that owns in-memory cvar state backed by config.cfg and d2c_preset.cfg.
/// </summary>
public interface ICvarSettingsProvider
{
    /// <summary>Current in-memory cvar state.</summary>
    CvarSettings Get();

    /// <summary>
    /// Update in-memory state and write changed cvars to their respective cfg files.
    /// Cvars with <see cref="CvarConfigSource.ConfigCfg"/> go to config.cfg;
    /// cvars with <see cref="CvarConfigSource.PresetCfg"/> go to d2c_preset.cfg.
    /// Always writes regardless of whether the game is running.
    /// Fires <see cref="CvarChanged"/>.
    /// </summary>
    void Update(CvarSettings settings);

    /// <summary>
    /// Load cvar values from config.cfg and d2c_preset.cfg into memory.
    /// Called on launcher startup and on game exit.
    /// Stores the game directory for subsequent <see cref="Update"/> calls.
    /// </summary>
    bool LoadFromConfigCfg(string gameDirectory);

    /// <summary>
    /// Returns current preset cvar values (those with <see cref="CvarConfigSource.PresetCfg"/>).
    /// Used by GameLaunchViewModel to write d2c_preset.cfg before launch.
    /// </summary>
    IReadOnlyDictionary<string, string> GetPresetCvars();

    /// <summary>Set by GameLaunchViewModel to indicate the game is running.</summary>
    bool IsGameRunning { get; set; }

    /// <summary>Fired when any cvar value changes (from UI or from cfg read).</summary>
    event Action? CvarChanged;
}
