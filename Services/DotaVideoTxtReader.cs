using System;
using System.Collections.Generic;
using System.IO;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

/// <summary>
/// Reads Dota's video.txt (Valve KeyValues format) and extracts display settings.
/// </summary>
public static class DotaVideoTxtReader
{
    private const string VideoTxtFileName = "video.txt";

    /// <summary>
    /// Reads video.txt and returns a populated <see cref="VideoSettings"/>.
    /// Returns null if the file doesn't exist or can't be read.
    /// </summary>
    public static VideoSettings? Read(string gameDirectory)
    {
        var path = Path.Combine(gameDirectory, "dota", "cfg", VideoTxtFileName);
        if (!File.Exists(path))
            return null;

        string content;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            content = reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            AppLog.Info($"DotaVideoTxtReader: could not read {path}: {ex.Message}");
            return null;
        }

        var kv = ParseKvPairs(content);
        var settings = new VideoSettings();

        if (kv.TryGetValue("setting.fullscreen", out var fs))
            settings.Fullscreen = fs == "1";

        if (kv.TryGetValue("setting.nowindowborder", out var nwb))
            settings.NoWindowBorder = nwb == "1";

        if (kv.TryGetValue("setting.defaultres", out var w) && int.TryParse(w, out var width) && width > 0)
            settings.Width = width;

        if (kv.TryGetValue("setting.defaultresheight", out var h) && int.TryParse(h, out var height) && height > 0)
            settings.Height = height;

        return settings;
    }

    internal static Dictionary<string, string> ParseKvPairs(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in content.Split('\n'))
        {
            var kv = ParseKvLine(line);
            if (kv.HasValue)
                result[kv.Value.Key] = kv.Value.Value;
        }
        return result;
    }

    internal static (string Key, string Value)? ParseKvLine(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith('"'))
            return null;

        var keyClose = trimmed.IndexOf('"', 1);
        if (keyClose < 0)
            return null;

        var key = trimmed[1..keyClose];
        var rest = trimmed[(keyClose + 1)..].TrimStart();

        if (!rest.StartsWith('"'))
            return null;

        var valClose = rest.IndexOf('"', 1);
        if (valClose < 0)
            return null;

        var value = rest[1..valClose];
        return (key, value);
    }
}
