using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

/// <summary>
/// Writes display settings into Dota's video.txt, preserving all other keys.
/// </summary>
public static class DotaVideoTxtWriter
{
    private const string VideoTxtFileName = "video.txt";

    /// <summary>
    /// Updates setting.fullscreen, setting.defaultres, setting.defaultresheight in video.txt.
    /// If video.txt doesn't exist, creates a minimal one.
    /// All other existing keys are preserved unchanged.
    /// </summary>
    public static void Write(string gameDirectory, VideoSettings settings)
    {
        var cfgDir = Path.Combine(gameDirectory, "dota", "cfg");
        Directory.CreateDirectory(cfgDir);
        var path = Path.Combine(cfgDir, VideoTxtFileName);

        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["setting.fullscreen"] = settings.Fullscreen ? "1" : "0",
            ["setting.nowindowborder"] = settings.NoWindowBorder ? "1" : "0",
            ["setting.defaultres"] = settings.Width.ToString(),
            ["setting.defaultresheight"] = settings.Height.ToString(),
            ["setting.aspectratiomode"] = AspectRatioMode(settings.Width, settings.Height).ToString(),
        };

        if (File.Exists(path))
        {
            UpdateExistingFile(path, updates);
        }
        else
        {
            CreateMinimalFile(path, updates);
        }
    }

    private static void UpdateExistingFile(string path, Dictionary<string, string> updates)
    {
        string content;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            content = reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            AppLog.Info($"DotaVideoTxtWriter: could not read {path}: {ex.Message}");
            return;
        }

        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();

        foreach (var rawLine in content.Split('\n'))
        {
            var kv = DotaVideoTxtReader.ParseKvLine(rawLine.TrimEnd('\r'));
            if (kv.HasValue && updates.ContainsKey(kv.Value.Key))
            {
                lines.Add(ReplaceValue(rawLine.TrimEnd('\r'), updates[kv.Value.Key]));
                written.Add(kv.Value.Key);
            }
            else
            {
                lines.Add(rawLine.TrimEnd('\r'));
            }
        }

        // Insert any keys not already present, before the closing brace
        foreach (var (k, v) in updates)
        {
            if (written.Contains(k))
                continue;

            var closeIdx = lines.LastIndexOf("}");
            var newLine = $"\t\"{k}\"\t\t\"{v}\"";
            if (closeIdx >= 0)
                lines.Insert(closeIdx, newLine);
            else
                lines.Add(newLine);
        }

        try
        {
            File.WriteAllText(path, string.Join("\n", lines), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            AppLog.Info($"DotaVideoTxtWriter: could not write {path}: {ex.Message}");
        }
    }

    private static void CreateMinimalFile(string path, Dictionary<string, string> updates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\"config\"");
        sb.AppendLine("{");
        foreach (var (k, v) in updates)
            sb.AppendLine($"\t\"{k}\"\t\t\"{v}\"");
        sb.Append("}");

        try
        {
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            AppLog.Info($"DotaVideoTxtWriter: could not create {path}: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the Source engine aspect ratio mode for a given resolution:
    /// 0 = 4:3, 1 = 16:9, 2 = 16:10.
    /// </summary>
    internal static int AspectRatioMode(int width, int height)
    {
        if (height == 0) return 1;
        var ratio = (double)width / height;
        if (Math.Abs(ratio - 16.0 / 10) < 0.05) return 2; // 16:10
        if (ratio > 1.6) return 1;                          // 16:9 and wider
        return 0;                                           // 4:3, 5:4, etc.
    }

    /// <summary>
    /// Replaces the value (second quoted string) in a KV line, preserving indentation.
    /// </summary>
    private static string ReplaceValue(string line, string newValue)
    {
        // Find the first quoted string (key)
        var firstOpen = line.IndexOf('"');
        if (firstOpen < 0) return line;
        var firstClose = line.IndexOf('"', firstOpen + 1);
        if (firstClose < 0) return line;

        // Find the second quoted string (value)
        var secondOpen = line.IndexOf('"', firstClose + 1);
        if (secondOpen < 0) return line;
        var secondClose = line.IndexOf('"', secondOpen + 1);
        if (secondClose < 0) return line;

        return line[..(secondOpen + 1)] + newValue + line[secondClose..];
    }
}
