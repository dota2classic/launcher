using System;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public interface IGameLaunchSettingsStorage
{
    GameLaunchSettings Get();
    void Save(GameLaunchSettings settings);

    /// <summary>
    /// Raised after <see cref="Save"/> writes settings (e.g. from config.cfg sync).
    /// Subscribers can refresh UI-bound properties.
    /// </summary>
    event Action? SettingsChanged;
}
