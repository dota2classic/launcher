using System;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

/// <summary>
/// Determines which cfg file a cvar is read from and written to.
/// </summary>
public enum CvarConfigSource
{
    /// <summary>config.cfg — managed by the game client. Used for cvars the game also writes.</summary>
    ConfigCfg,

    /// <summary>d2c_preset.cfg — managed only by the launcher. Used for cvars the game client never touches.</summary>
    PresetCfg,
}

/// <summary>
/// Central registry mapping Source engine cvar names to <see cref="CvarSettings"/> properties.
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

        new("dota_camera_disable_zoom",
            s => s.DisableCameraZoom ? "1" : "0",
            (s, v) => s.DisableCameraZoom = v is "1",
            IsEmpty: _ => false),

        new("dota_force_right_click_attack",
            s => s.ForceRightClickAttack ? "1" : "0",
            (s, v) => s.ForceRightClickAttack = v is "1",
            IsEmpty: _ => false),

        new("dota_player_auto_repeat_right_mouse",
            s => s.RightMouseAutoRepeat ? "1" : "0",
            (s, v) => s.RightMouseAutoRepeat = v is "1",
            IsEmpty: _ => false),

        new("dota_reset_camera_on_spawn",
            s => s.ResetCameraOnSpawn ? "1" : "0",
            (s, v) => s.ResetCameraOnSpawn = v is "1",
            IsEmpty: _ => false),

        new("dota_player_teleport_requires_halt",
            s => s.TeleportRequiresHalt ? "1" : "0",
            (s, v) => s.TeleportRequiresHalt = v is "1",
            IsEmpty: _ => false),

        new("dota_camera_distance",
            s => s.CameraDistance?.ToString() ?? "",
            (s, v) => s.CameraDistance = int.TryParse(v, out var n) ? Math.Clamp(n, 1000, 1600) : null,
            IsEmpty: s => !s.CameraDistance.HasValue,
            Source: CvarConfigSource.PresetCfg),

    ];
}

/// <param name="CvarName">Source engine console variable name.</param>
/// <param name="GetValue">Reads the current value from settings as a string.</param>
/// <param name="SetValue">Writes a parsed string value into settings.</param>
/// <param name="IsEmpty">Returns true if the setting should be omitted from cfg output (e.g. fps_max not set).</param>
/// <param name="Source">Which cfg file this cvar belongs to.</param>
public record CvarEntry(
    string CvarName,
    Func<CvarSettings, string> GetValue,
    Action<CvarSettings, string> SetValue,
    Func<CvarSettings, bool> IsEmpty,
    CvarConfigSource Source = CvarConfigSource.ConfigCfg);
