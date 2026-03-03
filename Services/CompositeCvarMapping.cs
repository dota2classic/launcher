using System;
using System.Collections.Generic;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

/// <summary>
/// Registry for settings that map to multiple cvars at once.
/// Parallel to <see cref="CvarMapping"/> which handles 1:1 mappings.
/// </summary>
public static class CompositeCvarMapping
{
    public static readonly CompositeCvarEntry[] Entries =
    [
        // Auto-attack mode: 1 enum → 2 cvars
        new(
            ["dota_player_units_auto_attack", "dota_player_units_auto_attack_after_spell"],
            s => s.AutoAttack switch
            {
                AutoAttackMode.Off => new Dictionary<string, string>
                {
                    ["dota_player_units_auto_attack"] = "0",
                    ["dota_player_units_auto_attack_after_spell"] = "0",
                },
                AutoAttackMode.Always => new Dictionary<string, string>
                {
                    ["dota_player_units_auto_attack"] = "1",
                    ["dota_player_units_auto_attack_after_spell"] = "1",
                },
                _ => new Dictionary<string, string> // AfterSpell (default)
                {
                    ["dota_player_units_auto_attack"] = "0",
                    ["dota_player_units_auto_attack_after_spell"] = "1",
                },
            },
            (s, values) =>
            {
                var aa = values.GetValueOrDefault("dota_player_units_auto_attack", "0");
                var aas = values.GetValueOrDefault("dota_player_units_auto_attack_after_spell", "1");
                s.AutoAttack = (aa, aas) switch
                {
                    ("1", _) => AutoAttackMode.Always,
                    ("0", "1") => AutoAttackMode.AfterSpell,
                    _ => AutoAttackMode.Off,
                };
            }),

    ];
}

/// <param name="CvarNames">Source engine cvar names involved.</param>
/// <param name="GetValues">Reads current values from settings as a name→value dictionary.</param>
/// <param name="SetValues">Writes parsed values from a name→value dictionary into settings.</param>
public record CompositeCvarEntry(
    string[] CvarNames,
    Func<CvarSettings, Dictionary<string, string>> GetValues,
    Action<CvarSettings, Dictionary<string, string>> SetValues);
