using System;
using System.IO;
using System.Text.Json;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public class SettingsStorage : ISettingsStorage
{
    private readonly string _filePath;
    private LauncherSettings? _cached;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public SettingsStorage()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "d2c-launcher");
        Directory.CreateDirectory(appFolder);
        _filePath = Path.Combine(appFolder, "launcher_settings.json");
    }

    public LauncherSettings Get()
    {
        if (_cached != null)
            return _cached;

        if (!File.Exists(_filePath))
        {
            _cached = new LauncherSettings();
            return _cached;
        }

        try
        {
            // TODO: File.ReadAllText blocks the calling thread. For settings this is fine
            // (tiny file, read once), but consider File.ReadAllTextAsync if Get() is ever
            // called from async context.
            var json = File.ReadAllText(_filePath);
            _cached = JsonSerializer.Deserialize<LauncherSettings>(json) ?? new LauncherSettings();
        }
        catch (Exception ex)
        {
            // TODO: Log this — silent fallback hides corruption or permission issues.
            // AppLog.Error("Failed to load settings, using defaults.", ex);
            _ = ex;
            _cached = new LauncherSettings();
        }

        return _cached;
    }

    public void Save(LauncherSettings settings)
    {
        _cached = settings;
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        try
        {
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            AppLog.Error($"[Settings] Failed to save settings to {_filePath}", ex);
        }
    }
}
