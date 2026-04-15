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
    private const string PresetCfgFileName = "d2c_preset.cfg";

    /// <summary>
    /// Server-managed immutable cfg, downloaded automatically into the game cfg folder.
    /// The launcher always execs it; the engine silently skips it if the file is absent.
    /// </summary>
    public const string ImmutableServerCfgArg = "+exec d2c_preset_config_imm.cfg";

    /// <summary>
    /// Preset cvars enforced on every launch. Not user-configurable.
    /// Exec'd after config.cfg so they always take effect.
    /// </summary>
    private static readonly string[] PresetLines =
    [
        "dota_embers 0",           // optimization: disable ambient embers
        "dota_full_ui 1",          // disable tutorial overlays
        "con_enable 1",            // always allow console access
        "dota_use_particle_fow 1", // prevent particle-based fog-of-war abuse
    ];

    /// <summary>
    /// Writes d2c_preset.cfg with hardcoded preset cvars plus any user-configurable preset cvars.
    /// Returns the +exec argument string, or null on failure.
    /// </summary>
    public static string? WritePreset(string gameDirectory, IReadOnlyDictionary<string, string>? userCvars = null)
    {
        var cfgDir = Path.Combine(gameDirectory, "dota", "cfg");
        try
        {
            Directory.CreateDirectory(cfgDir);
            var lines = new System.Collections.Generic.List<string>(PresetLines);
            if (userCvars != null)
                foreach (var (name, value) in userCvars)
                    lines.Add($"{name} \"{value}\"");
            var content = string.Join("\n", lines) + "\n";
            File.WriteAllText(Path.Combine(cfgDir, PresetCfgFileName), content, Encoding.UTF8);
            return $"+exec {PresetCfgFileName}";
        }
        catch
        {
            return null;
        }
    }

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
    /// Display settings (fullscreen, resolution) are managed via video.txt, not CLI args.
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
        parts.Add("-condebug");
        parts.Add("-netconport 27005");

        return string.Join(" ", parts);
    }
}
