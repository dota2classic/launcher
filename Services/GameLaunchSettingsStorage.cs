using System;
using System.IO;
using System.Text.Json;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public class GameLaunchSettingsStorage : IGameLaunchSettingsStorage
{
    private readonly string _filePath;
    private GameLaunchSettings? _cached;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public GameLaunchSettingsStorage()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "d2c-launcher");
        Directory.CreateDirectory(appFolder);
        _filePath = Path.Combine(appFolder, "launch_settings.json");
    }

    public GameLaunchSettings Get()
    {
        if (_cached != null)
            return _cached;

        if (!File.Exists(_filePath))
        {
            _cached = new GameLaunchSettings();
            return _cached;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            _cached = JsonSerializer.Deserialize<GameLaunchSettings>(json) ?? new GameLaunchSettings();
        }
        catch (Exception ex)
        {
            _ = ex;
            _cached = new GameLaunchSettings();
        }

        return _cached;
    }

    public void Save(GameLaunchSettings settings)
    {
        _cached = settings;
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}
