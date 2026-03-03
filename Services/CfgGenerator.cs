using System.Collections.Generic;
using System.IO;
using System.Text;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

/// <summary>
/// Generates d2c_launch.cfg from <see cref="GameLaunchSettings"/> and writes it to
/// {gameDirectory}/dota/cfg/. The file is a derived artifact — never edit it manually.
/// </summary>
public static class CfgGenerator
{
    private const string CfgFileName = "d2c_launch.cfg";

    /// <summary>
    /// Writes the cfg file and returns the +exec argument string, e.g. "+exec d2c_launch.cfg".
    /// Returns null if the cfg directory could not be resolved or created.
    /// </summary>
    public static string? Generate(GameLaunchSettings settings, string gameDirectory)
    {
        var cfgDir = Path.Combine(gameDirectory, "dota", "cfg");
        try
        {
            Directory.CreateDirectory(cfgDir);
        }
        catch
        {
            return null;
        }

        var lines = BuildCfgLines(settings);
        var content = string.Join("\n", lines) + "\n";
        File.WriteAllText(Path.Combine(cfgDir, CfgFileName), content, Encoding.UTF8);

        return $"+exec {CfgFileName}";
    }

    /// <summary>
    /// Builds the CLI argument string from settings (flags only, no +commands).
    /// </summary>
    public static string BuildCliArgs(GameLaunchSettings settings)
    {
        var parts = new List<string>();

        if (settings.NoVid)
            parts.Add("-novid");
        // Console is enabled via con_enable cvar in cfg, not -console flag
        // -console forces the console open at launch; con_enable just allows opening it with ~
        if (!string.IsNullOrWhiteSpace(settings.Language))
            parts.Add($"-language {settings.Language}");
        if (!string.IsNullOrWhiteSpace(settings.ExtraArgs))
            parts.Add(settings.ExtraArgs.Trim());

        return string.Join(" ", parts);
    }

    private static IEnumerable<string> BuildCfgLines(GameLaunchSettings settings)
    {
        foreach (var entry in CvarMapping.Entries)
        {
            if (entry.IsEmpty(settings))
                continue;
            yield return $"{entry.CvarName} {entry.GetValue(settings)}";
        }

        if (!string.IsNullOrWhiteSpace(settings.CustomCfgLines))
            yield return settings.CustomCfgLines.Trim();
    }
}
