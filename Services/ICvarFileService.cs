using System.Collections.Generic;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

/// <summary>
/// Injectable abstraction over <see cref="DotaCfgReader"/> and <see cref="DotaCfgWriter"/>.
/// Allows consumers to be unit-tested without real file I/O.
/// </summary>
public interface ICvarFileService
{
    /// <summary>
    /// Reads the cfg file identified by <paramref name="source"/> and applies any changed
    /// cvar values to <paramref name="settings"/>.
    /// Returns true if any setting was actually modified.
    /// </summary>
    bool ApplyToSettings(CvarSettings settings, string gameDirectory, CvarConfigSource source = CvarConfigSource.ConfigCfg);

    /// <summary>
    /// Updates (or appends) cvar values in config.cfg under <paramref name="gameDirectory"/>.
    /// </summary>
    void WriteCvars(string gameDirectory, Dictionary<string, string> cvars);
}
