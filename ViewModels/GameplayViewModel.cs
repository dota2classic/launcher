using System;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Models;
using d2c_launcher.Resources;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

public partial class GameplayViewModel : ViewModelBase
{
    private readonly ICvarSettingsProvider _cvarProvider;

    /// <summary>Delegate to push a cvar change to a running game. Set by parent VM.</summary>
    public Action<string, string>? PushCvar { get; set; }

    // ── Gameplay cvars ────────────────────────────────────────────────────────

    public bool DisableCameraZoom
    {
        get => GetBoolCvar(s => s.DisableCameraZoom);
        set => SetBoolCvar(s => s.DisableCameraZoom, (s, v) => s.DisableCameraZoom = v, value, "dota_camera_disable_zoom");
    }

    public bool ForceRightClickAttack
    {
        get => GetBoolCvar(s => s.ForceRightClickAttack);
        set => SetBoolCvar(s => s.ForceRightClickAttack, (s, v) => s.ForceRightClickAttack = v, value, "dota_force_right_click_attack");
    }

    public bool RightMouseAutoRepeat
    {
        get => GetBoolCvar(s => s.RightMouseAutoRepeat);
        set => SetBoolCvar(s => s.RightMouseAutoRepeat, (s, v) => s.RightMouseAutoRepeat = v, value, "dota_player_auto_repeat_right_mouse");
    }

    public bool ResetCameraOnSpawn
    {
        get => GetBoolCvar(s => s.ResetCameraOnSpawn);
        set => SetBoolCvar(s => s.ResetCameraOnSpawn, (s, v) => s.ResetCameraOnSpawn = v, value, "dota_reset_camera_on_spawn");
    }

    public bool TeleportRequiresHalt
    {
        get => GetBoolCvar(s => s.TeleportRequiresHalt);
        set => SetBoolCvar(s => s.TeleportRequiresHalt, (s, v) => s.TeleportRequiresHalt = v, value, "dota_player_teleport_requires_halt");
    }

    public int CameraDistance
    {
        get => _cvarProvider.Get().CameraDistance ?? 1134;
        set
        {
            var clamped = Math.Clamp(value, 1000, 1600);
            var s = _cvarProvider.Get();
            if (s.CameraDistance == clamped) return;
            s.CameraDistance = clamped;
            _cvarProvider.Update(s);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CameraDistanceText));
            PushCvar?.Invoke("dota_camera_distance", clamped.ToString());
        }
    }

    public string CameraDistanceText
    {
        get => CameraDistance.ToString();
        set
        {
            if (int.TryParse(value, out var n))
                CameraDistance = n;
            else
                OnPropertyChanged();
        }
    }

    public static string[] AutoAttackOptions { get; } = [Strings.Disabled, Strings.AfterSpell, Strings.Always];

    public int AutoAttackSelectedIndex
    {
        get => (int)_cvarProvider.Get().AutoAttack;
        set
        {
            var mode = (AutoAttackMode)value;
            var s = _cvarProvider.Get();
            if (s.AutoAttack == mode) return;
            s.AutoAttack = mode;
            _cvarProvider.Update(s);
            OnPropertyChanged();
            var values = CompositeCvarMapping.Entries[0].GetValues(s);
            foreach (var (cvar, v) in values)
                PushCvar?.Invoke(cvar, v);
        }
    }

    private static readonly string[] CvarPropertyNames =
    [
        nameof(DisableCameraZoom),
        nameof(ForceRightClickAttack),
        nameof(RightMouseAutoRepeat),
        nameof(ResetCameraOnSpawn),
        nameof(AutoAttackSelectedIndex),
        nameof(TeleportRequiresHalt),
        nameof(CameraDistance),
    ];

    public void RefreshFromCvarProvider()
    {
        foreach (var name in CvarPropertyNames)
            OnPropertyChanged(name);
    }

    // ── Constructor ────────────────────────────────────────────────────────────

    public GameplayViewModel(ICvarSettingsProvider cvarProvider)
    {
        _cvarProvider = cvarProvider;
    }

    // ── Cvar helpers ──────────────────────────────────────────────────────────

    private bool GetBoolCvar(Func<CvarSettings, bool> get) => get(_cvarProvider.Get());

    private void SetBoolCvar(
        Func<CvarSettings, bool> get,
        Action<CvarSettings, bool> set,
        bool value,
        string cvar,
        [CallerMemberName] string? propertyName = null)
    {
        var s = _cvarProvider.Get();
        if (get(s) == value) return;
        set(s, value);
        _cvarProvider.Update(s);
        OnPropertyChanged(propertyName);
        PushCvar?.Invoke(cvar, value ? "1" : "0");
    }
}
