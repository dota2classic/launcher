using System.IO;
using d2c_launcher.Models;
using d2c_launcher.Services;
using Xunit;

namespace d2c_launcher.Tests;

/// <summary>
/// Tests for DotaCfgReader using temporary directories that mirror the
/// real config layout: {gameDirectory}/dota/cfg/config.cfg
/// </summary>
public class DotaCfgReaderTests : IDisposable
{
    private readonly string _gameDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string _cfgDir;

    public DotaCfgReaderTests()
    {
        _cfgDir = Path.Combine(_gameDir, "dota", "cfg");
        Directory.CreateDirectory(_cfgDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_gameDir))
            Directory.Delete(_gameDir, recursive: true);
    }

    private void WriteCfg(string content)
        => File.WriteAllText(Path.Combine(_cfgDir, "config.cfg"), content);

    // ── ReadKnownCvars ────────────────────────────────────────────────────────

    [Fact]
    public void ReadKnownCvars_MissingFile_ReturnsEmpty()
    {
        var result = DotaCfgReader.ReadKnownCvars(_gameDir);
        Assert.Empty(result);
    }

    [Fact]
    public void ReadKnownCvars_EmptyFile_ReturnsEmpty()
    {
        WriteCfg("");
        var result = DotaCfgReader.ReadKnownCvars(_gameDir);
        Assert.Empty(result);
    }

    [Fact]
    public void ReadKnownCvars_IgnoresUnknownCvars()
    {
        WriteCfg("some_unknown_cvar \"value\"\n");
        var result = DotaCfgReader.ReadKnownCvars(_gameDir);
        Assert.Empty(result);
    }

    [Fact]
    public void ReadKnownCvars_IgnoresBindLines()
    {
        WriteCfg("bind \"q\" \"dota_ability_execute 0\"\n");
        var result = DotaCfgReader.ReadKnownCvars(_gameDir);
        Assert.Empty(result);
    }

    [Fact]
    public void ReadKnownCvars_IgnoresCommentLines()
    {
        WriteCfg("// con_enable \"1\"\n");
        var result = DotaCfgReader.ReadKnownCvars(_gameDir);
        Assert.Empty(result);
    }

    [Fact]
    public void ReadKnownCvars_IgnoresBlankLines()
    {
        WriteCfg("\n\n   \n");
        var result = DotaCfgReader.ReadKnownCvars(_gameDir);
        Assert.Empty(result);
    }

    [Fact]
    public void ReadKnownCvars_ParsesConEnable()
    {
        WriteCfg("con_enable \"1\"\n");
        var result = DotaCfgReader.ReadKnownCvars(_gameDir);
        Assert.Equal("1", result["con_enable"]);
    }

    [Fact]
    public void ReadKnownCvars_ParsesFpsMax()
    {
        WriteCfg("fps_max \"144\"\n");
        var result = DotaCfgReader.ReadKnownCvars(_gameDir);
        Assert.Equal("144", result["fps_max"]);
    }

    [Fact]
    public void ReadKnownCvars_ParsesMultipleKnownCvars()
    {
        WriteCfg(
            "fps_max \"240\"\n" +
            "con_enable \"0\"\n" +
            "dota_camera_disable_zoom \"1\"\n"
        );
        var result = DotaCfgReader.ReadKnownCvars(_gameDir);
        Assert.Equal("240", result["fps_max"]);
        Assert.Equal("0", result["con_enable"]);
        Assert.Equal("1", result["dota_camera_disable_zoom"]);
    }

    [Fact]
    public void ReadKnownCvars_ParsesAutoAttackCompositeCvars()
    {
        WriteCfg(
            "dota_player_units_auto_attack \"1\"\n" +
            "dota_player_units_auto_attack_after_spell \"1\"\n"
        );
        var result = DotaCfgReader.ReadKnownCvars(_gameDir);
        Assert.Equal("1", result["dota_player_units_auto_attack"]);
        Assert.Equal("1", result["dota_player_units_auto_attack_after_spell"]);
    }

    [Fact]
    public void ReadKnownCvars_IsCaseInsensitiveForCvarNames()
    {
        // config.cfg in practice uses lowercase, but the reader should be robust
        WriteCfg("CON_ENABLE \"1\"\n");
        var result = DotaCfgReader.ReadKnownCvars(_gameDir);
        Assert.True(result.ContainsKey("con_enable"));
    }

    [Fact]
    public void ReadKnownCvars_MixedContentFromRealConfig_ExtractsOnlyKnown()
    {
        // Excerpt from a real config.cfg (as provided in task description)
        const string excerpt = """
            unbindall
            bind "q" "dota_ability_execute 5"
            bind "r" "dota_ability_execute 0"
            con_enable "1"
            dota_camera_disable_zoom "1"
            dota_force_right_click_attack "1"
            dota_player_auto_repeat_right_mouse "1"
            dota_player_units_auto_attack "0"
            dota_player_units_auto_attack_after_spell "1"
            fps_max "120"
            sensitivity "3"
            """;
        WriteCfg(excerpt);

        var result = DotaCfgReader.ReadKnownCvars(_gameDir);

        // Known cvars present
        Assert.Equal("1", result["con_enable"]);
        Assert.Equal("1", result["dota_camera_disable_zoom"]);
        Assert.Equal("1", result["dota_force_right_click_attack"]);
        Assert.Equal("1", result["dota_player_auto_repeat_right_mouse"]);
        Assert.Equal("0", result["dota_player_units_auto_attack"]);
        Assert.Equal("1", result["dota_player_units_auto_attack_after_spell"]);
        Assert.Equal("120", result["fps_max"]);

        // Unknown cvars NOT present
        Assert.False(result.ContainsKey("sensitivity"));
        Assert.False(result.ContainsKey("unbindall"));
    }

    // ── ApplyToSettings ───────────────────────────────────────────────────────

    [Fact]
    public void ApplyToSettings_MissingFile_ReturnsFalse()
    {
        var settings = new CvarSettings();
        var changed = DotaCfgReader.ApplyToSettings(settings, _gameDir);
        Assert.False(changed);
    }

    [Fact]
    public void ApplyToSettings_NoKnownCvarsInFile_ReturnsFalse()
    {
        WriteCfg("sensitivity \"3\"\n");
        var settings = new CvarSettings();
        var changed = DotaCfgReader.ApplyToSettings(settings, _gameDir);
        Assert.False(changed);
    }

    [Fact]
    public void ApplyToSettings_ValueMatchesCurrent_ReturnsFalse()
    {
        // Default Console = true → con_enable "1". File says the same → no change.
        WriteCfg("con_enable \"1\"\n");
        var settings = new CvarSettings { Console = true };
        var changed = DotaCfgReader.ApplyToSettings(settings, _gameDir);
        Assert.False(changed);
    }

    [Fact]
    public void ApplyToSettings_DifferentConEnable_SetsPropertyAndReturnsTrue()
    {
        WriteCfg("con_enable \"0\"\n");
        var settings = new CvarSettings { Console = true };
        var changed = DotaCfgReader.ApplyToSettings(settings, _gameDir);
        Assert.True(changed);
        Assert.False(settings.Console);
    }

    [Fact]
    public void ApplyToSettings_SetsFpsMax()
    {
        WriteCfg("fps_max \"144\"\n");
        var settings = new CvarSettings { FpsMax = null };
        DotaCfgReader.ApplyToSettings(settings, _gameDir);
        Assert.Equal(144, settings.FpsMax);
    }

    [Fact]
    public void ApplyToSettings_SetsCameraDisableZoom()
    {
        WriteCfg("dota_camera_disable_zoom \"1\"\n");
        var settings = new CvarSettings { DisableCameraZoom = false };
        DotaCfgReader.ApplyToSettings(settings, _gameDir);
        Assert.True(settings.DisableCameraZoom);
    }

    [Fact]
    public void ApplyToSettings_SetsForceRightClickAttack()
    {
        WriteCfg("dota_force_right_click_attack \"1\"\n");
        var settings = new CvarSettings { ForceRightClickAttack = false };
        DotaCfgReader.ApplyToSettings(settings, _gameDir);
        Assert.True(settings.ForceRightClickAttack);
    }

    [Fact]
    public void ApplyToSettings_SetsRightMouseAutoRepeat()
    {
        WriteCfg("dota_player_auto_repeat_right_mouse \"0\"\n");
        var settings = new CvarSettings { RightMouseAutoRepeat = true };
        DotaCfgReader.ApplyToSettings(settings, _gameDir);
        Assert.False(settings.RightMouseAutoRepeat);
    }

    [Fact]
    public void ApplyToSettings_SetsResetCameraOnSpawn()
    {
        WriteCfg("dota_reset_camera_on_spawn \"0\"\n");
        var settings = new CvarSettings { ResetCameraOnSpawn = true };
        DotaCfgReader.ApplyToSettings(settings, _gameDir);
        Assert.False(settings.ResetCameraOnSpawn);
    }

    [Fact]
    public void ApplyToSettings_AutoAttack_Always_SetsModeAlways()
    {
        WriteCfg(
            "dota_player_units_auto_attack \"1\"\n" +
            "dota_player_units_auto_attack_after_spell \"1\"\n"
        );
        var settings = new CvarSettings { AutoAttack = AutoAttackMode.Off };
        DotaCfgReader.ApplyToSettings(settings, _gameDir);
        Assert.Equal(AutoAttackMode.Always, settings.AutoAttack);
    }

    [Fact]
    public void ApplyToSettings_AutoAttack_AfterSpell_SetsModeAfterSpell()
    {
        WriteCfg(
            "dota_player_units_auto_attack \"0\"\n" +
            "dota_player_units_auto_attack_after_spell \"1\"\n"
        );
        var settings = new CvarSettings { AutoAttack = AutoAttackMode.Off };
        DotaCfgReader.ApplyToSettings(settings, _gameDir);
        Assert.Equal(AutoAttackMode.AfterSpell, settings.AutoAttack);
    }

    [Fact]
    public void ApplyToSettings_AutoAttack_Off_SetsModeOff()
    {
        WriteCfg(
            "dota_player_units_auto_attack \"0\"\n" +
            "dota_player_units_auto_attack_after_spell \"0\"\n"
        );
        var settings = new CvarSettings { AutoAttack = AutoAttackMode.Always };
        DotaCfgReader.ApplyToSettings(settings, _gameDir);
        Assert.Equal(AutoAttackMode.Off, settings.AutoAttack);
    }

    [Fact]
    public void ApplyToSettings_RealConfigExcerpt_ParsesCorrectly()
    {
        // Exact lines from the config.cfg provided in the task description
        const string excerpt = """
            con_enable "1"
            dota_camera_disable_zoom "1"
            dota_force_right_click_attack "1"
            dota_player_auto_repeat_right_mouse "1"
            dota_player_units_auto_attack "0"
            dota_player_units_auto_attack_after_spell "1"
            dota_hud_colorblind "0"
            """;
        WriteCfg(excerpt);

        var settings = new CvarSettings
        {
            Console = false,
            DisableCameraZoom = false,
            ForceRightClickAttack = false,
            RightMouseAutoRepeat = false,
            AutoAttack = AutoAttackMode.Off,
        };

        var changed = DotaCfgReader.ApplyToSettings(settings, _gameDir);

        Assert.True(changed);
        Assert.True(settings.Console);
        Assert.True(settings.DisableCameraZoom);
        Assert.True(settings.ForceRightClickAttack);
        Assert.True(settings.RightMouseAutoRepeat);
        Assert.Equal(AutoAttackMode.AfterSpell, settings.AutoAttack);
    }
}
