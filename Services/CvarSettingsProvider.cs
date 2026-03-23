using System;
using System.Collections.Generic;
using System.Linq;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public class CvarSettingsProvider : ICvarSettingsProvider
{
    private readonly ICvarFileService _fileService;
    private CvarSettings _settings = new();
    private string? _gameDirectory;

    public CvarSettingsProvider(ICvarFileService fileService)
    {
        _fileService = fileService;
    }

    public bool IsGameRunning { get; set; }

    public event Action? CvarChanged;

    public CvarSettings Get() => _settings;

    public void Update(CvarSettings settings)
    {
        _settings = settings;

        if (!string.IsNullOrWhiteSpace(_gameDirectory))
        {
            try
            {
                var configCvars = BuildCvarDictionary(settings, CvarConfigSource.ConfigCfg);
                _fileService.WriteCvars(_gameDirectory, configCvars);
            }
            catch (Exception ex)
            {
                AppLog.Error("CvarSettingsProvider: failed to write config.cfg", ex);
            }

            try
            {
                var presetCvars = BuildCvarDictionary(settings, CvarConfigSource.PresetCfg);
                CfgGenerator.WritePreset(_gameDirectory, presetCvars);
            }
            catch (Exception ex)
            {
                AppLog.Error("CvarSettingsProvider: failed to write d2c_preset.cfg", ex);
            }
        }

        CvarChanged?.Invoke();
    }

    public bool LoadFromConfigCfg(string gameDirectory)
    {
        _gameDirectory = gameDirectory;

        var changed = _fileService.ApplyToSettings(_settings, gameDirectory, CvarConfigSource.ConfigCfg);
        var presetChanged = _fileService.ApplyToSettings(_settings, gameDirectory, CvarConfigSource.PresetCfg);
        changed = changed || presetChanged;

        if (changed)
            CvarChanged?.Invoke();

        // Enforce cl_cloud_settings 0 every time we read config.cfg.
        // This prevents retail Dota 2 from merging its cloud config with D2C's local config.
        try
        {
            _fileService.WriteCvars(gameDirectory, new Dictionary<string, string> { ["cl_cloud_settings"] = "0" });
        }
        catch (Exception ex)
        {
            AppLog.Error("CvarSettingsProvider: failed to enforce cl_cloud_settings=0", ex);
        }

        return changed;
    }

    /// <summary>
    /// Builds a dictionary of cvar names → current string values for the given source file.
    /// For <see cref="CvarConfigSource.ConfigCfg"/> also includes cl_cloud_settings=0 and composite cvars.
    /// </summary>
    private static Dictionary<string, string> BuildCvarDictionary(CvarSettings settings, CvarConfigSource source)
    {
        var cvars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (source == CvarConfigSource.ConfigCfg)
            cvars["cl_cloud_settings"] = "0";

        foreach (var entry in CvarMapping.Entries)
        {
            if (entry.Source != source)
                continue;
            if (entry.IsEmpty(settings))
                continue;
            cvars[entry.CvarName] = entry.GetValue(settings);
        }

        if (source == CvarConfigSource.ConfigCfg)
        {
            foreach (var entry in CompositeCvarMapping.Entries)
                foreach (var (name, value) in entry.GetValues(settings))
                    cvars[name] = value;
        }

        return cvars;
    }

    /// <summary>
    /// Returns the current preset cvars (for <see cref="CvarConfigSource.PresetCfg"/>) as a dictionary.
    /// Used by <see cref="GameLaunchViewModel"/> when writing d2c_preset.cfg before launch.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetPresetCvars()
        => BuildCvarDictionary(_settings, CvarConfigSource.PresetCfg);
}
