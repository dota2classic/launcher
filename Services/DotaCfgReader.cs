using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

/// <summary>
/// Reads Dota's config.cfg and extracts values for known cvars (defined in <see cref="CvarMapping"/>).
/// </summary>
public static class DotaCfgReader
{
    private const string ConfigFileName = "config.cfg";

    /// <summary>
    /// Reads config.cfg from <paramref name="gameDirectory"/> and returns values for known cvars.
    /// Returns an empty dictionary on any IO error (file locked, missing, etc.).
    /// </summary>
    public static Dictionary<string, string> ReadKnownCvars(string gameDirectory)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var configPath = Path.Combine(gameDirectory, "dota", "cfg", ConfigFileName);

        if (!File.Exists(configPath))
            return result;

        var knownNames = CvarMapping.Entries
            .Select(e => e.CvarName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] lines;
        try
        {
            // Use FileShare.ReadWrite to avoid conflicts with the game process.
            using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            lines = reader.ReadToEnd().Split('\n');
        }
        catch (Exception ex)
        {
            AppLog.Info($"DotaCfgReader: could not read {configPath}: {ex.Message}");
            return result;
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("//"))
                continue;

            // Source engine config.cfg format: cvarname "value"
            var spaceIdx = line.IndexOf(' ');
            if (spaceIdx <= 0)
                continue;

            var name = line[..spaceIdx];
            if (!knownNames.Contains(name))
                continue;

            var value = line[(spaceIdx + 1)..].Trim().Trim('"');
            result[name] = value;
        }

        return result;
    }

    /// <summary>
    /// Reads config.cfg and applies any changed cvar values to <paramref name="settings"/>.
    /// Returns true if any setting was actually modified.
    /// </summary>
    public static bool ApplyToSettings(GameLaunchSettings settings, string gameDirectory)
    {
        var cvars = ReadKnownCvars(gameDirectory);
        if (cvars.Count == 0)
            return false;

        var changed = false;
        foreach (var entry in CvarMapping.Entries)
        {
            if (!cvars.TryGetValue(entry.CvarName, out var fileValue))
                continue;

            var currentValue = entry.GetValue(settings);
            if (string.Equals(currentValue, fileValue, StringComparison.OrdinalIgnoreCase))
                continue;

            entry.SetValue(settings, fileValue);
            changed = true;
        }

        return changed;
    }
}
