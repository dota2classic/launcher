using System;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public class VideoSettingsProvider : IVideoSettingsProvider
{
    private VideoSettings _settings = new();
    private string? _gameDirectory;

    public VideoSettings Get() => _settings;

    public void Update(VideoSettings settings)
    {
        _settings = settings;

        if (!string.IsNullOrWhiteSpace(_gameDirectory))
        {
            try
            {
                DotaVideoTxtWriter.Write(_gameDirectory, settings);
            }
            catch (Exception ex)
            {
                AppLog.Error("VideoSettingsProvider: failed to write video.txt", ex);
            }
        }
    }

    public void LoadFromVideoTxt(string gameDirectory)
    {
        _gameDirectory = gameDirectory;

        var loaded = DotaVideoTxtReader.Read(gameDirectory);
        if (loaded != null)
            _settings = loaded;
    }
}
