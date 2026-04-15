using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

/// <summary>
/// Manages the Dotakeys keybind profile for Dotaclassic (App ID 570).
///
/// On first launch: copies dotakeys_personal.lst → dotakeys_684.lst (migration).
/// Every launch: patches the ScreenshotSettings block to enforce the chatwheel action binding.
/// Never modifies dotakeys_personal.lst after migration.
/// </summary>
[SupportedOSPlatform("windows")]
public class DotakeysProfileService : IDotakeysProfileService
{
    private const string ProfileFileName = "dotakeys_684.lst";
    private const string SourceFileName  = "dotakeys_personal.lst";
    private const string DotaAppId       = "570";

    private const string TargetAction      = "+chatwheel_hero";
    private const string TargetDescription = "#DOTA_ChatWheel_HeroClassicPlus";

    public bool PrepareProfile(ulong steamId32)
    {
        var cfgDir = ResolveProfileDirectory(steamId32);
        if (cfgDir == null)
        {
            AppLog.Error("[DotakeysProfile] Cannot resolve Steam userdata cfg directory.");
            return false;
        }

        var profilePath = Path.Combine(cfgDir, ProfileFileName);
        var sourcePath  = Path.Combine(cfgDir, SourceFileName);

        // ── One-time migration ─────────────────────────────────────────────────
        if (!File.Exists(profilePath))
        {
            if (!File.Exists(sourcePath))
            {
                AppLog.Error($"[DotakeysProfile] Neither {ProfileFileName} nor {SourceFileName} found in: {cfgDir}");
                return false;
            }

            try
            {
                File.Copy(sourcePath, profilePath, overwrite: false);
                AppLog.Info($"[DotakeysProfile] Migrated {SourceFileName} → {ProfileFileName}");
            }
            catch (Exception ex)
            {
                AppLog.Error($"[DotakeysProfile] Failed to copy {SourceFileName} → {ProfileFileName}", ex);
                return false;
            }
        }

        // ── Per-launch patch ───────────────────────────────────────────────────
        return PatchProfile(profilePath);
    }

    private static bool PatchProfile(string profilePath)
    {
        string content;
        try
        {
            content = File.ReadAllText(profilePath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            AppLog.Error($"[DotakeysProfile] Failed to read {profilePath}", ex);
            return false;
        }

        var (patched, changed) = ApplyScreenshotPatch(content);

        if (!changed)
        {
            AppLog.Info("[DotakeysProfile] Already up-to-date, no write needed.");
            return true;
        }

        // Atomic write: write to temp file, then replace the original
        var tempPath = profilePath + ".launcher.tmp";
        try
        {
            File.WriteAllText(tempPath, patched, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, profilePath, overwrite: true);
            AppLog.Info("[DotakeysProfile] Patched ScreenshotSettings successfully.");
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("[DotakeysProfile] Failed to write patched profile.", ex);
            try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
            return false;
        }
    }

    /// <summary>
    /// Finds the ScreenshotSettings block and replaces the Action and Description values
    /// if they differ from the target values. Returns the (possibly modified) text and
    /// a flag indicating whether anything actually changed.
    /// </summary>
    private static (string text, bool changed) ApplyScreenshotPatch(string content)
    {
        var blockRange = FindBlock(content, "ScreenshotSettings");
        if (blockRange == null)
        {
            AppLog.Warn("[DotakeysProfile] ScreenshotSettings block not found — skipping patch.");
            return (content, false);
        }

        var (blockStart, blockEnd) = blockRange.Value;
        var blockText = content[blockStart..blockEnd];

        var changed = false;
        var patchedBlock = PatchKeyInBlock(blockText, "Action",      TargetAction,      ref changed);
            patchedBlock = PatchKeyInBlock(patchedBlock, "Description", TargetDescription, ref changed);

        if (!changed)
            return (content, false);

        return (string.Concat(content.AsSpan(0, blockStart), patchedBlock, content.AsSpan(blockEnd)), true);
    }

    /// <summary>
    /// Returns the character range [start, end) of the block body delimited by the
    /// outermost { … } that follows the <paramref name="blockName"/> header, or null
    /// if the block is not found or is malformed.
    /// </summary>
    private static (int start, int end)? FindBlock(string content, string blockName)
    {
        var headerPattern = new Regex($@"""{Regex.Escape(blockName)}""\s*\{{", RegexOptions.IgnoreCase);
        var m = headerPattern.Match(content);
        if (!m.Success)
            return null;

        // The { character is the last char of the match
        var braceStart = m.Index + m.Length - 1;

        var depth = 0;
        for (var i = braceStart; i < content.Length; i++)
        {
            if (content[i] == '{') depth++;
            else if (content[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return (braceStart, i + 1);
            }
        }

        AppLog.Warn("[DotakeysProfile] ScreenshotSettings block has no matching closing brace.");
        return null;
    }

    /// <summary>
    /// Replaces the value of <paramref name="key"/> inside a VDF block snippet.
    /// Sets <paramref name="changed"/> to true if the replacement was made.
    /// Replaces only the first occurrence (VDF keys are unique within a block).
    /// </summary>
    private static string PatchKeyInBlock(string block, string key, string newValue, ref bool changed)
    {
        var pattern = new Regex($@"(""{Regex.Escape(key)}""\s+)""([^""]*)""");
        var localChanged = false;
        var result = pattern.Replace(block, m =>
        {
            if (string.Equals(m.Groups[2].Value, newValue, StringComparison.Ordinal))
                return m.Value; // already correct

            localChanged = true;
            return $"{m.Groups[1].Value}\"{newValue}\"";
        }, count: 1);

        if (localChanged)
            changed = true;

        return result;
    }

    /// <summary>
    /// Resolves the path to <c>Steam/userdata/&lt;steamId32&gt;/570/remote/cfg/</c>
    /// using the Steam install path from the registry.
    /// Returns null if the path cannot be determined or does not exist on disk.
    /// </summary>
    private static string? ResolveProfileDirectory(ulong steamId32)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam", false);
            var steamPath = key?.GetValue("SteamPath")?.ToString();
            if (string.IsNullOrEmpty(steamPath))
            {
                AppLog.Warn("[DotakeysProfile] SteamPath not found in registry.");
                return null;
            }

            var cfgDir = Path.Combine(steamPath, "userdata", steamId32.ToString(), DotaAppId, "remote", "cfg");
            if (!Directory.Exists(cfgDir))
            {
                AppLog.Warn($"[DotakeysProfile] Cfg directory does not exist: {cfgDir}");
                return null;
            }

            return cfgDir;
        }
        catch (Exception ex)
        {
            AppLog.Error("[DotakeysProfile] Failed to resolve Steam cfg directory.", ex);
            return null;
        }
    }
}
