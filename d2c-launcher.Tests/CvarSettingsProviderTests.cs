using System.IO;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Tests.Fakes;
using Xunit;

namespace d2c_launcher.Tests;

/// <summary>
/// Unit tests for <see cref="CvarSettingsProvider"/> using an in-memory
/// <see cref="FakeCvarFileService"/> so no real files are needed.
/// </summary>
public class CvarSettingsProviderTests : IDisposable
{
    // A real temp dir is needed only for CfgGenerator.WritePreset (the preset write in Update).
    private readonly string _gameDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly FakeCvarFileService _fileService = new();

    public CvarSettingsProviderTests()
    {
        Directory.CreateDirectory(Path.Combine(_gameDir, "dota", "cfg"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_gameDir))
            Directory.Delete(_gameDir, recursive: true);
    }

    private CvarSettingsProvider MakeProvider() => new(_fileService);

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_WithNoGameDirectory_FiresCvarChangedWithoutWritingFiles()
    {
        var provider = MakeProvider();
        var fired = false;
        provider.CvarChanged += () => fired = true;

        provider.Update(new CvarSettings { FpsMax = 120 });

        Assert.True(fired);
        Assert.Empty(_fileService.WriteLog);
    }

    [Fact]
    public void Update_WithGameDirectory_AlwaysWritesCloudSettingsZeroToConfigCfg()
    {
        var provider = MakeProvider();
        provider.LoadFromConfigCfg(_gameDir); // sets game directory

        _fileService.WriteLog.Clear();
        provider.Update(new CvarSettings());

        var configWrite = Assert.Single(_fileService.WriteLog, w =>
            w.Cvars.ContainsKey("cl_cloud_settings"));
        Assert.Equal("0", configWrite.Cvars["cl_cloud_settings"]);
    }

    [Fact]
    public void Update_WithGameDirectory_WritesConfigCfgEntriesToConfigCfg()
    {
        var provider = MakeProvider();
        provider.LoadFromConfigCfg(_gameDir);

        _fileService.WriteLog.Clear();
        provider.Update(new CvarSettings { FpsMax = 144, Console = true });

        var configWrite = _fileService.WriteLog.Find(w => w.Cvars.ContainsKey("fps_max"));
        Assert.NotNull(configWrite);
        Assert.Equal("144", configWrite.Cvars["fps_max"]);
        Assert.Equal("1", configWrite.Cvars["con_enable"]);
    }

    [Fact]
    public void Update_WithGameDirectory_PresetCfgEntryAbsentFromConfigCfgWrite()
    {
        // dota_camera_distance is Source=PresetCfg — must not appear in config.cfg write
        var provider = MakeProvider();
        provider.LoadFromConfigCfg(_gameDir);

        _fileService.WriteLog.Clear();
        provider.Update(new CvarSettings { CameraDistance = 1200 });

        var configWrite = _fileService.WriteLog.Find(w => w.Cvars.ContainsKey("cl_cloud_settings"));
        Assert.NotNull(configWrite);
        Assert.DoesNotContain("dota_camera_distance", configWrite.Cvars.Keys);
    }

    [Fact]
    public void Update_WithGameDirectory_CompositeCvarsIncludedInConfigCfgWrite()
    {
        var provider = MakeProvider();
        provider.LoadFromConfigCfg(_gameDir);

        _fileService.WriteLog.Clear();
        provider.Update(new CvarSettings { AutoAttack = AutoAttackMode.Always });

        var configWrite = _fileService.WriteLog.Find(w => w.Cvars.ContainsKey("cl_cloud_settings"));
        Assert.NotNull(configWrite);
        Assert.Equal("1", configWrite.Cvars["dota_player_units_auto_attack"]);
        Assert.Equal("1", configWrite.Cvars["dota_player_units_auto_attack_after_spell"]);
    }

    [Fact]
    public void Update_AlwaysFiresCvarChanged()
    {
        var provider = MakeProvider();
        provider.LoadFromConfigCfg(_gameDir);
        var count = 0;
        provider.CvarChanged += () => count++;

        provider.Update(new CvarSettings());
        provider.Update(new CvarSettings());

        Assert.Equal(2, count);
    }

    // ── LoadFromConfigCfg ─────────────────────────────────────────────────────

    [Fact]
    public void LoadFromConfigCfg_WhenSettingsChanged_ReturnsTrueAndFiresCvarChanged()
    {
        _fileService.OnApplyToSettings = (settings, _, source) =>
        {
            if (source == CvarConfigSource.ConfigCfg)
            {
                settings.FpsMax = 120;
                return true;
            }
            return false;
        };
        var provider = MakeProvider();
        var fired = false;
        provider.CvarChanged += () => fired = true;

        var changed = provider.LoadFromConfigCfg(_gameDir);

        Assert.True(changed);
        Assert.True(fired);
        Assert.Equal(120, provider.Get().FpsMax);
    }

    [Fact]
    public void LoadFromConfigCfg_WhenNothingChanged_ReturnsFalseAndDoesNotFireCvarChanged()
    {
        // OnApplyToSettings defaults to returning false
        var provider = MakeProvider();
        var fired = false;
        provider.CvarChanged += () => fired = true;

        var changed = provider.LoadFromConfigCfg(_gameDir);

        Assert.False(changed);
        Assert.False(fired);
    }

    [Fact]
    public void LoadFromConfigCfg_WhenPresetChanged_ReturnsTrueAndFiresCvarChanged()
    {
        _fileService.OnApplyToSettings = (settings, _, source) =>
        {
            if (source == CvarConfigSource.PresetCfg)
            {
                settings.CameraDistance = 1300;
                return true;
            }
            return false;
        };
        var provider = MakeProvider();
        var fired = false;
        provider.CvarChanged += () => fired = true;

        var changed = provider.LoadFromConfigCfg(_gameDir);

        Assert.True(changed);
        Assert.True(fired);
    }

    [Fact]
    public void LoadFromConfigCfg_AlwaysWritesCloudSettingsZeroRegardlessOfChanges()
    {
        // Even when nothing changed, cl_cloud_settings=0 must be written
        var provider = MakeProvider();

        provider.LoadFromConfigCfg(_gameDir);

        var cloudWrite = Assert.Single(_fileService.WriteLog);
        Assert.Equal("0", cloudWrite.Cvars["cl_cloud_settings"]);
    }

    [Fact]
    public void LoadFromConfigCfg_SetsGameDirectorySoSubsequentUpdateWritesFiles()
    {
        var provider = MakeProvider();
        provider.LoadFromConfigCfg(_gameDir);
        _fileService.WriteLog.Clear();

        provider.Update(new CvarSettings());

        Assert.NotEmpty(_fileService.WriteLog);
    }

    // ── GetPresetCvars ────────────────────────────────────────────────────────

    [Fact]
    public void GetPresetCvars_ReturnsOnlyPresetSourceEntries()
    {
        var provider = MakeProvider();
        // FpsMax is ConfigCfg; CameraDistance is PresetCfg
        var settings = provider.Get();
        settings.FpsMax = 120;
        settings.CameraDistance = 1200;
        provider.Update(settings);

        var preset = provider.GetPresetCvars();

        Assert.Contains("dota_camera_distance", preset.Keys);
        Assert.DoesNotContain("fps_max", preset.Keys);
        Assert.DoesNotContain("cl_cloud_settings", preset.Keys);
    }

    [Fact]
    public void GetPresetCvars_EmptyWhenNoCameraDistanceSet()
    {
        var provider = MakeProvider();

        var preset = provider.GetPresetCvars();

        Assert.Empty(preset);
    }
}
