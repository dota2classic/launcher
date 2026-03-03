using System;
using System.Collections.Generic;
using System.Linq;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public class CvarSettingsProvider : ICvarSettingsProvider
{
    private CvarSettings _settings = new();
    private string? _gameDirectory;

    public bool IsGameRunning { get; set; }

    public event Action? CvarChanged;

    public CvarSettings Get() => _settings;

    public void Update(CvarSettings settings)
    {
        _settings = settings;

        if (!IsGameRunning && !string.IsNullOrWhiteSpace(_gameDirectory))
        {
            try
            {
                var cvars = BuildCvarDictionary(settings);
                DotaCfgWriter.WriteCvars(_gameDirectory, cvars);
            }
            catch (Exception ex)
            {
                AppLog.Error("CvarSettingsProvider: failed to write config.cfg", ex);
            }
        }

        CvarChanged?.Invoke();
    }

    public bool LoadFromConfigCfg(string gameDirectory)
    {
        _gameDirectory = gameDirectory;

        var changed = DotaCfgReader.ApplyToSettings(_settings, gameDirectory);
        if (changed)
            CvarChanged?.Invoke();

        // Enforce cl_cloud_settings 0 every time we read config.cfg.
        // This prevents retail Dota 2 from merging its cloud config with D2C's local config.
        try
        {
            DotaCfgWriter.WriteCvars(gameDirectory, new Dictionary<string, string> { ["cl_cloud_settings"] = "0" });
        }
        catch (Exception ex)
        {
            AppLog.Error("CvarSettingsProvider: failed to enforce cl_cloud_settings=0", ex);
        }

        return changed;
    }

    /// <summary>
    /// Builds a dictionary of all managed cvar names → current string values.
    /// Always includes cl_cloud_settings=0 to prevent retail Dota 2 cloud sync from conflicting.
    /// </summary>
    private static Dictionary<string, string> BuildCvarDictionary(CvarSettings settings)
    {
        var cvars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cl_cloud_settings"] = "0",
        };

        foreach (var entry in CvarMapping.Entries)
        {
            if (entry.IsEmpty(settings))
                continue;
            cvars[entry.CvarName] = entry.GetValue(settings);
        }

        foreach (var entry in CompositeCvarMapping.Entries)
        {
            foreach (var (name, value) in entry.GetValues(settings))
                cvars[name] = value;
        }

        return cvars;
    }
}
