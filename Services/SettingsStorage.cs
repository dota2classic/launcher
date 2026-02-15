using System;
using System.IO;
using System.Text.Json;
using d2c_launcher.Models;

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
            var json = File.ReadAllText(_filePath);
            _cached = JsonSerializer.Deserialize<LauncherSettings>(json) ?? new LauncherSettings();
        }
        catch
        {
            _cached = new LauncherSettings();
        }

        return _cached;
    }

    public void Save(LauncherSettings settings)
    {
        _cached = settings;
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}
