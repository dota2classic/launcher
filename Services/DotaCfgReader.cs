using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

/// <summary>
/// Reads Dota's config.cfg and extracts values for known cvars.
/// </summary>
public static class DotaCfgReader
{
    private const string ConfigFileName = "config.cfg";

    /// <summary>
    /// Reads config.cfg and returns values for known cvars.
    /// Returns an empty dictionary on any IO error (file locked, missing, etc.).
    /// </summary>
    public static Dictionary<string, string> ReadKnownCvars(string gameDirectory)
    {
        var cvars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var configPath = Path.Combine(gameDirectory, "dota", "cfg", ConfigFileName);

        if (!File.Exists(configPath))
            return cvars;

        var knownCvarNames = CvarMapping.Entries
            .Select(e => e.CvarName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Also include composite cvar names
        foreach (var entry in CompositeCvarMapping.Entries)
            foreach (var name in entry.CvarNames)
                knownCvarNames.Add(name);

        string[] lines;
        try
        {
            using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            lines = reader.ReadToEnd().Split('\n');
        }
        catch (Exception ex)
        {
            AppLog.Info($"DotaCfgReader: could not read {configPath}: {ex.Message}");
            return cvars;
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("//"))
                continue;

            // Skip bind lines
            if (line.StartsWith("bind ", StringComparison.OrdinalIgnoreCase))
                continue;

            // Parse cvar lines: cvarname "value"
            var spaceIdx = line.IndexOf(' ');
            if (spaceIdx <= 0)
                continue;

            var name = line[..spaceIdx];
            if (!knownCvarNames.Contains(name))
                continue;

            var value = line[(spaceIdx + 1)..].Trim().Trim('"');
            cvars[name] = value;
        }

        return cvars;
    }

    /// <summary>
    /// Reads config.cfg and applies any changed cvar values to <paramref name="settings"/>.
    /// Returns true if any setting was actually modified.
    /// </summary>
    public static bool ApplyToSettings(CvarSettings settings, string gameDirectory)
    {
        var cvars = ReadKnownCvars(gameDirectory);
        if (cvars.Count == 0)
            return false;

        var changed = false;

        // 1:1 cvars
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

        // Composite cvars
        foreach (var entry in CompositeCvarMapping.Entries)
        {
            var fileValues = new Dictionary<string, string>();
            foreach (var name in entry.CvarNames)
                if (cvars.TryGetValue(name, out var v))
                    fileValues[name] = v;

            if (fileValues.Count > 0)
            {
                var currentValues = entry.GetValues(settings);
                var differs = fileValues.Any(kv =>
                    !currentValues.TryGetValue(kv.Key, out var cur)
                    || !string.Equals(cur, kv.Value, StringComparison.OrdinalIgnoreCase));

                if (differs)
                {
                    entry.SetValues(settings, fileValues);
                    changed = true;
                }
            }
        }

        return changed;
    }
}
