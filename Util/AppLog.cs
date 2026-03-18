using System;
using System.IO;
using d2c_launcher.Services;

namespace d2c_launcher.Util;

public static class AppLog
{
    private static readonly object Sync = new();
    private static readonly string LogPath = BuildLogPath();

    public static void Info(string message)
    {
        Write("INFO", message, null);
        FaroTelemetryService.TrackLog("info", message);
    }

    public static void Warn(string message, Exception? ex = null)
    {
        Write("WARN", message, ex);
        FaroTelemetryService.TrackLog("warn", message);
    }

    public static void Error(string message, Exception? ex = null)
    {
        Write("ERR", message, ex);
        if (ex != null)
            FaroTelemetryService.TrackException(ex);
        else
            FaroTelemetryService.TrackLog("error", message);
    }

    private static void Write(string level, string message, Exception? ex)
    {
        var line = $"[{DateTime.UtcNow:O}] [{level}] {message}";
        if (ex != null)
            line += $" :: {ex.GetType().Name}: {ex.Message}";

        try { Console.WriteLine(line); } catch { }

        try
        {
            lock (Sync)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Swallow logger failures.
        }
    }

    private static string BuildLogPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "d2c-launcher");
        Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, "launcher.log");
    }
}
