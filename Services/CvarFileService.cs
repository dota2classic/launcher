using System.Collections.Generic;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

/// <summary>
/// Default implementation of <see cref="ICvarFileService"/> — delegates to the static
/// <see cref="DotaCfgReader"/> and <see cref="DotaCfgWriter"/> used in production.
/// </summary>
public class CvarFileService : ICvarFileService
{
    public bool ApplyToSettings(CvarSettings settings, string gameDirectory,
        CvarConfigSource source = CvarConfigSource.ConfigCfg)
        => DotaCfgReader.ApplyToSettings(settings, gameDirectory, source);

    public void WriteCvars(string gameDirectory, Dictionary<string, string> cvars)
        => DotaCfgWriter.WriteCvars(gameDirectory, cvars);
}
