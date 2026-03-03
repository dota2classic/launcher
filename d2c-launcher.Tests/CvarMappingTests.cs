using d2c_launcher.Models;
using d2c_launcher.Services;
using Xunit;

namespace d2c_launcher.Tests;

/// <summary>
/// Tests for CvarMapping (1:1 cvar ↔ CvarSettings property) and
/// CompositeCvarMapping (1 enum → N cvars).
/// </summary>
public class CvarMappingTests
{
    // ── fps_max ───────────────────────────────────────────────────────────────

    [Fact]
    public void FpsMax_IsEmpty_WhenNotSet()
    {
        var entry = Entry("fps_max");
        var s = new CvarSettings { FpsMax = null };
        Assert.True(entry.IsEmpty(s));
    }

    [Fact]
    public void FpsMax_IsNotEmpty_WhenSet()
    {
        var entry = Entry("fps_max");
        var s = new CvarSettings { FpsMax = 120 };
        Assert.False(entry.IsEmpty(s));
    }

    [Fact]
    public void FpsMax_GetValue_ReturnsStringOfValue()
    {
        var entry = Entry("fps_max");
        var s = new CvarSettings { FpsMax = 240 };
        Assert.Equal("240", entry.GetValue(s));
    }

    [Fact]
    public void FpsMax_GetValue_ReturnsEmptyWhenNull()
    {
        var entry = Entry("fps_max");
        var s = new CvarSettings { FpsMax = null };
        Assert.Equal("", entry.GetValue(s));
    }

    [Fact]
    public void FpsMax_SetValue_ParsesPositiveInteger()
    {
        var entry = Entry("fps_max");
        var s = new CvarSettings();
        entry.SetValue(s, "144");
        Assert.Equal(144, s.FpsMax);
    }

    [Fact]
    public void FpsMax_SetValue_SetsNullForZero()
    {
        var entry = Entry("fps_max");
        var s = new CvarSettings { FpsMax = 120 };
        entry.SetValue(s, "0");
        Assert.Null(s.FpsMax);
    }

    [Fact]
    public void FpsMax_SetValue_SetsNullForNegative()
    {
        var entry = Entry("fps_max");
        var s = new CvarSettings { FpsMax = 120 };
        entry.SetValue(s, "-1");
        Assert.Null(s.FpsMax);
    }

    [Fact]
    public void FpsMax_SetValue_SetsNullForNonNumeric()
    {
        var entry = Entry("fps_max");
        var s = new CvarSettings { FpsMax = 120 };
        entry.SetValue(s, "abc");
        Assert.Null(s.FpsMax);
    }

    // ── con_enable ────────────────────────────────────────────────────────────

    [Fact]
    public void ConEnable_IsNeverEmpty()
    {
        var entry = Entry("con_enable");
        Assert.False(entry.IsEmpty(new CvarSettings { Console = true }));
        Assert.False(entry.IsEmpty(new CvarSettings { Console = false }));
    }

    [Fact]
    public void ConEnable_GetValue_TrueReturns1()
    {
        var entry = Entry("con_enable");
        Assert.Equal("1", entry.GetValue(new CvarSettings { Console = true }));
    }

    [Fact]
    public void ConEnable_GetValue_FalseReturns0()
    {
        var entry = Entry("con_enable");
        Assert.Equal("0", entry.GetValue(new CvarSettings { Console = false }));
    }

    [Fact]
    public void ConEnable_SetValue_1SetsTrue()
    {
        var entry = Entry("con_enable");
        var s = new CvarSettings { Console = false };
        entry.SetValue(s, "1");
        Assert.True(s.Console);
    }

    [Fact]
    public void ConEnable_SetValue_0SetsFalse()
    {
        var entry = Entry("con_enable");
        var s = new CvarSettings { Console = true };
        entry.SetValue(s, "0");
        Assert.False(s.Console);
    }

    // ── dota_camera_disable_zoom ──────────────────────────────────────────────

    [Fact]
    public void DisableCameraZoom_GetValue_TrueReturns1()
    {
        var entry = Entry("dota_camera_disable_zoom");
        Assert.Equal("1", entry.GetValue(new CvarSettings { DisableCameraZoom = true }));
    }

    [Fact]
    public void DisableCameraZoom_GetValue_FalseReturns0()
    {
        var entry = Entry("dota_camera_disable_zoom");
        Assert.Equal("0", entry.GetValue(new CvarSettings { DisableCameraZoom = false }));
    }

    [Fact]
    public void DisableCameraZoom_SetValue_Roundtrip()
    {
        var entry = Entry("dota_camera_disable_zoom");
        var s = new CvarSettings { DisableCameraZoom = false };
        entry.SetValue(s, "1");
        Assert.True(s.DisableCameraZoom);
        entry.SetValue(s, "0");
        Assert.False(s.DisableCameraZoom);
    }

    // ── dota_force_right_click_attack ─────────────────────────────────────────

    [Fact]
    public void ForceRightClickAttack_Roundtrip()
    {
        var entry = Entry("dota_force_right_click_attack");
        var s = new CvarSettings { ForceRightClickAttack = false };
        entry.SetValue(s, "1");
        Assert.True(s.ForceRightClickAttack);
        Assert.Equal("1", entry.GetValue(s));
    }

    // ── dota_player_auto_repeat_right_mouse ───────────────────────────────────

    [Fact]
    public void RightMouseAutoRepeat_Roundtrip()
    {
        var entry = Entry("dota_player_auto_repeat_right_mouse");
        var s = new CvarSettings { RightMouseAutoRepeat = false };
        entry.SetValue(s, "1");
        Assert.True(s.RightMouseAutoRepeat);
        Assert.Equal("1", entry.GetValue(s));
    }

    // ── dota_reset_camera_on_spawn ────────────────────────────────────────────

    [Fact]
    public void ResetCameraOnSpawn_Roundtrip()
    {
        var entry = Entry("dota_reset_camera_on_spawn");
        var s = new CvarSettings { ResetCameraOnSpawn = true };
        entry.SetValue(s, "0");
        Assert.False(s.ResetCameraOnSpawn);
        Assert.Equal("0", entry.GetValue(s));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static CvarEntry Entry(string name)
    {
        var entry = Array.Find(CvarMapping.Entries, e =>
            string.Equals(e.CvarName, name, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(entry);
        return entry!;
    }
}

public class CompositeCvarMappingTests
{
    private static CompositeCvarEntry AutoAttackEntry =>
        CompositeCvarMapping.Entries[0]; // only one entry for now

    // ── GetValues ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetValues_Off_BothCvarsAre0()
    {
        var s = new CvarSettings { AutoAttack = AutoAttackMode.Off };
        var values = AutoAttackEntry.GetValues(s);
        Assert.Equal("0", values["dota_player_units_auto_attack"]);
        Assert.Equal("0", values["dota_player_units_auto_attack_after_spell"]);
    }

    [Fact]
    public void GetValues_AfterSpell_AutoAttack0_AfterSpell1()
    {
        var s = new CvarSettings { AutoAttack = AutoAttackMode.AfterSpell };
        var values = AutoAttackEntry.GetValues(s);
        Assert.Equal("0", values["dota_player_units_auto_attack"]);
        Assert.Equal("1", values["dota_player_units_auto_attack_after_spell"]);
    }

    [Fact]
    public void GetValues_Always_BothCvarsAre1()
    {
        var s = new CvarSettings { AutoAttack = AutoAttackMode.Always };
        var values = AutoAttackEntry.GetValues(s);
        Assert.Equal("1", values["dota_player_units_auto_attack"]);
        Assert.Equal("1", values["dota_player_units_auto_attack_after_spell"]);
    }

    // ── SetValues ─────────────────────────────────────────────────────────────

    [Fact]
    public void SetValues_1_Any_SetsAlways()
    {
        var s = new CvarSettings { AutoAttack = AutoAttackMode.Off };
        AutoAttackEntry.SetValues(s, new()
        {
            ["dota_player_units_auto_attack"] = "1",
            ["dota_player_units_auto_attack_after_spell"] = "0",
        });
        Assert.Equal(AutoAttackMode.Always, s.AutoAttack);
    }

    [Fact]
    public void SetValues_0_1_SetsAfterSpell()
    {
        var s = new CvarSettings { AutoAttack = AutoAttackMode.Off };
        AutoAttackEntry.SetValues(s, new()
        {
            ["dota_player_units_auto_attack"] = "0",
            ["dota_player_units_auto_attack_after_spell"] = "1",
        });
        Assert.Equal(AutoAttackMode.AfterSpell, s.AutoAttack);
    }

    [Fact]
    public void SetValues_0_0_SetsOff()
    {
        var s = new CvarSettings { AutoAttack = AutoAttackMode.Always };
        AutoAttackEntry.SetValues(s, new()
        {
            ["dota_player_units_auto_attack"] = "0",
            ["dota_player_units_auto_attack_after_spell"] = "0",
        });
        Assert.Equal(AutoAttackMode.Off, s.AutoAttack);
    }

    // ── Roundtrip ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(AutoAttackMode.Off)]
    [InlineData(AutoAttackMode.AfterSpell)]
    [InlineData(AutoAttackMode.Always)]
    public void SetValues_GetValues_Roundtrip(AutoAttackMode mode)
    {
        var original = new CvarSettings { AutoAttack = mode };
        var dict = AutoAttackEntry.GetValues(original);

        var restored = new CvarSettings { AutoAttack = AutoAttackMode.Off };
        AutoAttackEntry.SetValues(restored, dict);

        Assert.Equal(mode, restored.AutoAttack);
    }

    // ── CvarNames ─────────────────────────────────────────────────────────────

    [Fact]
    public void CvarNames_ContainsBothAutoAttackKeys()
    {
        var names = AutoAttackEntry.CvarNames;
        Assert.Contains("dota_player_units_auto_attack", names);
        Assert.Contains("dota_player_units_auto_attack_after_spell", names);
    }
}
