using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

/// <summary>
/// Writes cvar values into Dota's config.cfg, preserving existing lines.
/// </summary>
public static class DotaCfgWriter
{
    private const string ConfigFileName = "config.cfg";

    /// <summary>
    /// Updates (or appends) cvar values in config.cfg.
    /// If config.cfg doesn't exist, creates it with just the managed cvars.
    /// </summary>
    public static void WriteCvars(string gameDirectory, Dictionary<string, string> cvars)
    {
        var cfgDir = Path.Combine(gameDirectory, "dota", "cfg");
        Directory.CreateDirectory(cfgDir);

        // Normalize to case-insensitive so file keys like "CON_ENABLE" match dict keys like "con_enable".
        var cvarsCi = new Dictionary<string, string>(cvars, StringComparer.OrdinalIgnoreCase);

        var configPath = Path.Combine(cfgDir, ConfigFileName);
        var lines = new List<string>();
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(configPath))
        {
            string[] existingLines;
            try
            {
                using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                existingLines = reader.ReadToEnd().Split('\n');
            }
            catch (Exception ex)
            {
                AppLog.Info($"DotaCfgWriter: could not read {configPath}: {ex.Message}");
                return;
            }

            foreach (var rawLine in existingLines)
            {
                var trimmed = rawLine.Trim();

                // Try to match cvar lines: "cvarname value" or "cvarname \"value\""
                var spaceIdx = trimmed.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    var name = trimmed[..spaceIdx];
                    if (cvarsCi.TryGetValue(name, out var newValue))
                    {
                        lines.Add($"{name} \"{newValue}\"");
                        written.Add(name);
                        continue;
                    }
                }

                // Preserve line as-is (comments, binds, unknown cvars, empty lines)
                lines.Add(rawLine.TrimEnd('\r'));
            }
        }

        // Append any cvars not found in the existing file
        foreach (var (name, value) in cvarsCi)
        {
            if (!written.Contains(name))
                lines.Add($"{name} \"{value}\"");
        }

        // Strip trailing empty lines accumulated from previous writes
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);

        try
        {
            File.WriteAllText(configPath, string.Join("\n", lines) + "\n", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            AppLog.Info($"DotaCfgWriter: could not write {configPath}: {ex.Message}");
        }
    }
}
