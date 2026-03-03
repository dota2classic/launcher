using System;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Models;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IGameLaunchSettingsStorage _launchStorage;
    private readonly ICvarSettingsProvider _cvarProvider;

    /// <summary>Delegate to push a cvar change to a running game. Set by parent VM.</summary>
    public Action<string, string>? PushCvar { get; set; }

    // ── Launch parameters ──────────────────────────────────────────────────

    public static string[] AvailableLanguages { get; } = ["russian", "english"];

    public string SelectedLanguage
    {
        get => _launchStorage.Get().Language;
        set
        {
            var s = _launchStorage.Get();
            if (s.Language == value) return;
            s.Language = value;
            _launchStorage.Save(s);
            OnPropertyChanged();
        }
    }

    public bool NoVid
    {
        get => _launchStorage.Get().NoVid;
        set
        {
            var s = _launchStorage.Get();
            if (s.NoVid == value) return;
            s.NoVid = value;
            _launchStorage.Save(s);
            OnPropertyChanged();
        }
    }

    // ── Gameplay (cvars backed by config.cfg) ─────────────────────────────

    public bool DisableCameraZoom
    {
        get => _cvarProvider.Get().DisableCameraZoom;
        set
        {
            var s = _cvarProvider.Get();
            if (s.DisableCameraZoom == value) return;
            s.DisableCameraZoom = value;
            _cvarProvider.Update(s);
            OnPropertyChanged();
            PushCvar?.Invoke("dota_camera_disable_zoom", value ? "1" : "0");
        }
    }

    public bool ForceRightClickAttack
    {
        get => _cvarProvider.Get().ForceRightClickAttack;
        set
        {
            var s = _cvarProvider.Get();
            if (s.ForceRightClickAttack == value) return;
            s.ForceRightClickAttack = value;
            _cvarProvider.Update(s);
            OnPropertyChanged();
            PushCvar?.Invoke("dota_force_right_click_attack", value ? "1" : "0");
        }
    }

    public bool RightMouseAutoRepeat
    {
        get => _cvarProvider.Get().RightMouseAutoRepeat;
        set
        {
            var s = _cvarProvider.Get();
            if (s.RightMouseAutoRepeat == value) return;
            s.RightMouseAutoRepeat = value;
            _cvarProvider.Update(s);
            OnPropertyChanged();
            PushCvar?.Invoke("dota_player_auto_repeat_right_mouse", value ? "1" : "0");
        }
    }

    public bool ResetCameraOnSpawn
    {
        get => _cvarProvider.Get().ResetCameraOnSpawn;
        set
        {
            var s = _cvarProvider.Get();
            if (s.ResetCameraOnSpawn == value) return;
            s.ResetCameraOnSpawn = value;
            _cvarProvider.Update(s);
            OnPropertyChanged();
            PushCvar?.Invoke("dota_reset_camera_on_spawn", value ? "1" : "0");
        }
    }

    // ── Auto-attack mode ───────────────────────────────────────────────────

    public static string[] AutoAttackOptions { get; } = ["Выключена", "После заклинания", "Всегда"];

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

            // Push both constituent cvars to the running game
            var values = CompositeCvarMapping.Entries[0].GetValues(s);
            foreach (var (cvar, v) in values)
                PushCvar?.Invoke(cvar, v);
        }
    }

    // ── Constructor ────────────────────────────────────────────────────────

    public SettingsViewModel(IGameLaunchSettingsStorage launchStorage, ICvarSettingsProvider cvarProvider)
    {
        _launchStorage = launchStorage;
        _cvarProvider = cvarProvider;
    }

    /// <summary>
    /// Refreshes all UI-bound cvar properties.
    /// Called when config.cfg is re-read (e.g. after game exit).
    /// </summary>
    public void RefreshFromCvarProvider()
    {
        OnPropertyChanged(nameof(DisableCameraZoom));
        OnPropertyChanged(nameof(ForceRightClickAttack));
        OnPropertyChanged(nameof(RightMouseAutoRepeat));
        OnPropertyChanged(nameof(ResetCameraOnSpawn));
        OnPropertyChanged(nameof(AutoAttackSelectedIndex));
    }
}
