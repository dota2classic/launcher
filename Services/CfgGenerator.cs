using System.Collections.Generic;
using System.IO;
using System.Text;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

/// <summary>
/// Generates d2c_launch.cfg for custom cfg lines and builds CLI arguments.
/// Cvars are now written directly to config.cfg by <see cref="CvarSettingsProvider"/>.
/// </summary>
public static class CfgGenerator
{
    private const string CfgFileName = "d2c_launch.cfg";

    /// <summary>
    /// Writes d2c_launch.cfg if <see cref="GameLaunchSettings.CustomCfgLines"/> is set.
    /// Returns the +exec argument string, or null if no cfg file is needed.
    /// </summary>
    public static string? Generate(GameLaunchSettings settings, string gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(settings.CustomCfgLines))
            return null;

        var cfgDir = Path.Combine(gameDirectory, "dota", "cfg");
        try
        {
            Directory.CreateDirectory(cfgDir);
        }
        catch
        {
            return null;
        }

        var content = settings.CustomCfgLines.Trim() + "\n";
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
        if (!string.IsNullOrWhiteSpace(settings.Language))
            parts.Add($"-language {settings.Language}");
        if (!string.IsNullOrWhiteSpace(settings.ExtraArgs))
            parts.Add(settings.ExtraArgs.Trim());

        return string.Join(" ", parts);
    }
}
