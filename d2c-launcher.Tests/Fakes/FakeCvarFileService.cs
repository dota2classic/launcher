using System.Collections.Generic;
using d2c_launcher.Models;
using d2c_launcher.Services;

namespace d2c_launcher.Tests.Fakes;

/// <summary>
/// In-memory fake for <see cref="ICvarFileService"/>.
/// Records all <see cref="WriteCvars"/> calls and lets tests control what
/// <see cref="ApplyToSettings"/> returns and writes into settings.
/// </summary>
internal sealed class FakeCvarFileService : ICvarFileService
{
    /// <summary>All WriteCvars calls in order: (gameDirectory, cvars).</summary>
    public List<(string GameDirectory, Dictionary<string, string> Cvars)> WriteLog { get; } = [];

    /// <summary>
    /// When set, ApplyToSettings calls this delegate and returns its result.
    /// Receives (settings, gameDirectory, source) — can mutate settings directly.
    /// Defaults to returning false (no changes).
    /// </summary>
    public Func<CvarSettings, string, CvarConfigSource, bool>? OnApplyToSettings { get; set; }

    public bool ApplyToSettings(CvarSettings settings, string gameDirectory, CvarConfigSource source)
        => OnApplyToSettings?.Invoke(settings, gameDirectory, source) ?? false;

    public void WriteCvars(string gameDirectory, Dictionary<string, string> cvars)
        => WriteLog.Add((gameDirectory, new Dictionary<string, string>(cvars, StringComparer.OrdinalIgnoreCase)));
}
