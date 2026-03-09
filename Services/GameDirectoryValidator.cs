using System.IO;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

/// <summary>
/// Validates that a given directory is suitable for Dotaclassic (patch 6.84).
/// Rejects new Dota 2 (Source 2) installations and old Dota builds from a different patch.
/// </summary>
public static class GameDirectoryValidator
{
    /// <summary>PatchVersion value in dota/steam.inf for the Dotaclassic 6.84 build.</summary>
    private const int ExpectedPatchVersion = 41;

    /// <summary>
    /// Returns true if the directory is acceptable: either a fresh/empty folder (for new downloads),
    /// or a Dotaclassic 6.84 installation.
    /// Returns false when the directory is positively identified as the wrong version.
    /// </summary>
    public static bool IsAcceptable(string dir, out string? error)
    {
        AppLog.Info($"[GameDirValidator] Checking directory: {dir}");

        // New Dota 2 (Source 2) is identified by game/dota/gameinfo.gi.
        // The user may have picked an exe inside a subdirectory (e.g. game/bin/win64),
        // so walk up to 5 parent levels to find the actual install root.
        var checkDir = dir;
        for (int i = 0; i <= 5; i++)
        {
            if (checkDir == null) break;
            var source2Marker = Path.Combine(checkDir, "game", "dota", "gameinfo.gi");
            AppLog.Info($"[GameDirValidator] Source 2 marker check (level {i}): {File.Exists(source2Marker)} ({source2Marker})");
            if (File.Exists(source2Marker))
            {
                error = "Выбранная папка содержит новую Dota 2 (Source 2). Пожалуйста, выберите папку с Dotaclassic.";
                AppLog.Info($"[GameDirValidator] Rejected: Source 2 installation detected at {checkDir}.");
                return false;
            }
            checkDir = Path.GetDirectoryName(checkDir);
        }

        // If the directory has dota/gameinfo.txt, it's an old Dota install — verify patch version.
        // No steam.inf → assume it's already our patch (D2C manages it).
        var gameInfoPath = Path.Combine(dir, "dota", "gameinfo.txt");
        AppLog.Info($"[GameDirValidator] Source 1 gameinfo exists: {File.Exists(gameInfoPath)} ({gameInfoPath})");
        if (File.Exists(gameInfoPath))
        {
            var steamInf = Path.Combine(dir, "dota", "steam.inf");
            AppLog.Info($"[GameDirValidator] steam.inf exists: {File.Exists(steamInf)} ({steamInf})");
            if (File.Exists(steamInf))
            {
                foreach (var line in File.ReadLines(steamInf))
                {
                    if (line.StartsWith("PatchVersion=", System.StringComparison.OrdinalIgnoreCase))
                    {
                        var value = line["PatchVersion=".Length..].Trim();
                        AppLog.Info($"[GameDirValidator] PatchVersion={value} (expected {ExpectedPatchVersion})");
                        if (!int.TryParse(value, out var patchVersion) || patchVersion != ExpectedPatchVersion)
                        {
                            error = $"Выбранная папка содержит другой патч Dota 2 (версия {value}). Dotaclassic использует патч 6.84. Выберите правильную папку или скачайте игру заново.";
                            AppLog.Info($"[GameDirValidator] Rejected: wrong PatchVersion.");
                            return false;
                        }
                        break;
                    }
                }
            }
        }

        AppLog.Info($"[GameDirValidator] Accepted.");
        error = null;
        return true;
    }
}
