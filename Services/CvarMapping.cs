using System;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

/// <summary>
/// Central registry mapping Source engine cvar names to <see cref="GameLaunchSettings"/> properties.
/// Used by CfgGenerator (write), DotaCfgReader (read-back), and live console push.
/// </summary>
public static class CvarMapping
{
    public static readonly CvarEntry[] Entries =
    [
        new("fps_max",
            s => s.FpsMax?.ToString() ?? "",
            (s, v) => s.FpsMax = int.TryParse(v, out var n) && n > 0 ? n : null,
            IsEmpty: s => !s.FpsMax.HasValue),

        new("con_enable",
            s => s.Console ? "1" : "0",
            (s, v) => s.Console = v is "1",
            IsEmpty: _ => false),

        new("dota_hud_colorblind",
            s => s.ColorblindMode ? "1" : "0",
            (s, v) => s.ColorblindMode = v is "1",
            IsEmpty: _ => false),
    ];
}

/// <param name="CvarName">Source engine console variable name.</param>
/// <param name="GetValue">Reads the current value from settings as a string.</param>
/// <param name="SetValue">Writes a parsed string value into settings.</param>
/// <param name="IsEmpty">Returns true if the setting should be omitted from cfg output (e.g. fps_max not set).</param>
public record CvarEntry(
    string CvarName,
    Func<GameLaunchSettings, string> GetValue,
    Action<GameLaunchSettings, string> SetValue,
    Func<GameLaunchSettings, bool> IsEmpty);
