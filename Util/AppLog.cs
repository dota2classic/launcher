using System;
using System.IO;

namespace d2c_launcher.Util;

public static class AppLog
{
    private static readonly object Sync = new();
    private static readonly string LogPath = BuildLogPath();

    public static void Info(string message) => Write("INFO", message, null);
    public static void Error(string message, Exception? ex = null) => Write("ERR", message, ex);

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
