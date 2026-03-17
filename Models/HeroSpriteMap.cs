using System.Collections.Generic;

namespace d2c_launcher.Models;

/// <summary>
/// Maps hero short names (e.g. "axe", "antimage") to their pixel offsets
/// in Assets/Images/minimap_hero_sheet.png (32×32 per icon, 16 columns).
/// Offsets are from the CSS background-position values (negated).
/// </summary>
public static class HeroSpriteMap
{
    public static readonly IReadOnlyDictionary<string, (int X, int Y)> Offsets =
        new Dictionary<string, (int, int)>
        {
            { "axe",                  (  0,   0) },
            { "antimage",             ( 32,   0) },
            { "crystal_maiden",       ( 64,   0) },
            { "ancient_apparition",   ( 96,   0) },
            { "batrider",             (128,   0) },
            { "beastmaster",          (160,   0) },
            { "bloodseeker",          (192,   0) },
            { "bounty_hunter",        (224,   0) },
            { "broodmother",          (256,   0) },
            { "chen",                 (288,   0) },
            { "dark_seer",            (320,   0) },
            { "dazzle",               (352,   0) },
            { "death_prophet",        (384,   0) },
            { "doom_bringer",         (416,   0) },
            { "dragon_knight",        (448,   0) },
            { "alchemist",            (480,   0) },

            { "drow_ranger",          (  0,  32) },
            { "earthshaker",          ( 32,  32) },
            { "enchantress",          ( 64,  32) },
            { "enigma",               ( 96,  32) },
            { "faceless_void",        (128,  32) },
            { "furion",               (160,  32) },
            { "huskar",               (192,  32) },
            { "juggernaut",           (224,  32) },
            { "kunkka",               (256,  32) },
            { "leshrac",              (288,  32) },
            { "lich",                 (320,  32) },
            { "life_stealer",         (352,  32) },
            { "lina",                 (384,  32) },
            { "lion",                 (416,  32) },
            { "mirana",               (448,  32) },
            { "morphling",            (480,  32) },

            { "necrolyte",            (  0,  64) },
            { "nevermore",            ( 32,  64) },
            { "night_stalker",        ( 64,  64) },
            { "omniknight",           ( 96,  64) },
            { "puck",                 (128,  64) },
            { "pudge",                (160,  64) },
            { "pugna",                (192,  64) },
            { "queenofpain",          (224,  64) },
            { "rattletrap",           (256,  64) },
            { "razor",                (288,  64) },
            { "riki",                 (320,  64) },
            { "sand_king",            (352,  64) },
            { "shadow_shaman",        (384,  64) },
            { "silencer",             (416,  64) },
            { "skeleton_king",        (448,  64) },
            { "slardar",              (480,  64) },

            { "sniper",               (  0,  96) },
            { "spectre",              ( 32,  96) },
            { "spirit_breaker",       ( 64,  96) },
            { "storm_spirit",         ( 96,  96) },
            { "sven",                 (128,  96) },
            { "tidehunter",           (160,  96) },
            { "tinker",               (192,  96) },
            { "tiny",                 (224,  96) },
            { "jakiro",               (256,  96) },
            { "ursa",                 (288,  96) },
            { "vengefulspirit",       (320,  96) },
            { "venomancer",           (352,  96) },
            { "viper",                (384,  96) },
            { "warlock",              (416,  96) },
            { "weaver",               (448,  96) },
            { "windrunner",           (480,  96) },

            { "witch_doctor",         (  0, 128) },
            { "zuus",                 ( 32, 128) },
            { "invoker",              ( 64, 128) },
            { "clinkz",               ( 96, 128) },
            { "obsidian_destroyer",   (128, 128) },
            { "bane",                 (160, 128) },
            { "shadow_demon",         (192, 128) },
            { "lycan",                (224, 128) },
            { "lone_druid",           (256, 128) },
            { "brewmaster",           (288, 128) },
            { "brewmaster_fire",      (320, 128) },
            { "brewmaster_storm",     (352, 128) },
            { "brewmaster_earth",     (384, 128) },
            { "phantom_lancer",       (416, 128) },
            { "chaos_knight",         (448, 128) },
            { "phantom_assassin",     (480, 128) },

            { "treant",               (  0, 160) },
            { "luna",                 ( 32, 160) },
            { "ogre_magi",            ( 64, 160) },
            { "gyrocopter",           ( 96, 160) },
            { "rubick",               (128, 160) },
            { "wisp",                 (160, 160) },
            { "disruptor",            (192, 160) },
            { "undying",              (224, 160) },
            { "naga_siren",           (256, 160) },
            { "templar_assassin",     (288, 160) },
            { "nyx_assassin",         (320, 160) },
            { "keeper_of_the_light",  (352, 160) },
            { "visage",               (384, 160) },
            { "magnataur",            (416, 160) },
            { "meepo",                (448, 160) },
            { "centaur",              (480, 160) },

            { "slark",                (  0, 192) },
            { "medusa",               ( 32, 192) },
            { "shredder",             ( 64, 192) },
            { "troll_warlord",        ( 96, 192) },
            { "tusk",                 (128, 192) },
            { "bristleback",          (160, 192) },
            { "skywrath_mage",        (192, 192) },
            { "elder_titan",          (224, 192) },
            { "abaddon",              (256, 192) },
            { "ember_spirit",         (288, 192) },
            { "legion_commander",     (320, 192) },
            { "earth_spirit",         (352, 192) },
            { "terrorblade",          (384, 192) },
            { "phoenix",              (416, 192) },
            { "techies",              (448, 192) },

            { "oracle",               ( 64, 224) },
            { "lina_alt1",            (480, 224) },
            { "legion_commander_alt1",(448, 224) },
            { "terrorblade_alt1",     (416, 224) },
            { "techies_alt1",         (384, 224) },
            { "nevermore_alt1",       (352, 224) },
            { "phantom_assassin_alt1",(320, 224) },
        };

    public static bool TryGetOffset(string heroName, out (int X, int Y) offset)
    {
        var key = heroName.StartsWith("npc_dota_hero_")
            ? heroName["npc_dota_hero_".Length..]
            : heroName;
        return Offsets.TryGetValue(key, out offset);
    }
}
