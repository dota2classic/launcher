using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Models;
using d2c_launcher.Resources;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

public record ResolutionItem(string Label, int W, int H, bool IsNative);

public partial class GameSettingsViewModel : ViewModelBase
{
    private readonly ICvarSettingsProvider _cvarProvider;
    private readonly IVideoSettingsProvider _videoProvider;

    /// <summary>Delegate to push a cvar change to a running game. Set by parent VM.</summary>
    public Action<string, string>? PushCvar { get; set; }

    // ── Video settings ─────────────────────────────────────────────────────────

    public bool Fullscreen
    {
        get => _videoProvider.Get().Fullscreen;
        set
        {
            var s = _videoProvider.Get();
            if (s.Fullscreen == value) return;
            s.Fullscreen = value;
            _videoProvider.Update(s);
            OnPropertyChanged();
        }
    }

    public bool NoWindowBorder
    {
        get => _videoProvider.Get().NoWindowBorder;
        set
        {
            var s = _videoProvider.Get();
            if (s.NoWindowBorder == value) return;
            s.NoWindowBorder = value;
            _videoProvider.Update(s);
            OnPropertyChanged();
        }
    }

    // ── Resolution picker ─────────────────────────────────────────────────────

    private static readonly (string Label, int W, int H)[] AllResolutions =
    [
        // 4:3
        ("4:3", 640, 480), ("4:3", 800, 600), ("4:3", 1024, 768), ("4:3", 1280, 960), ("4:3", 1600, 1200),
        // 5:4
        ("5:4", 1280, 1024),
        // 16:10
        ("16:10", 1280, 800), ("16:10", 1440, 900), ("16:10", 1680, 1050), ("16:10", 1920, 1200),
        // 16:9
        ("16:9", 1280, 720), ("16:9", 1366, 768), ("16:9", 1600, 900), ("16:9", 1920, 1080),
        ("16:9", 2560, 1440), ("16:9", 3840, 2160),
    ];

    public static string[] AvailableAspectRatios { get; } = ["4:3", "5:4", "16:10", "16:9"];

    private (int W, int H) _monitorSize = (0, 0);
    private int _selectedAspectRatioIndex = 3; // default 16:9

    public int SelectedAspectRatioIndex
    {
        get => _selectedAspectRatioIndex;
        set
        {
            if (_selectedAspectRatioIndex == value) return;
            _selectedAspectRatioIndex = value;
            var resolutions = ResolutionsForCurrentRatio();
            if (resolutions.Count > 0)
            {
                var vs = _videoProvider.Get();
                vs.Width = resolutions[0].W;
                vs.Height = resolutions[0].H;
                _videoProvider.Update(vs);
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(AvailableResolutions));
            OnPropertyChanged(nameof(SelectedResolution));
        }
    }

    public void SetMonitorSize(int w, int h)
    {
        _monitorSize = (w, h);
        var ratio = InferAspectRatio(w, h);
        var ratioIdx = Array.IndexOf(AvailableAspectRatios, ratio);
        if (ratioIdx >= 0) _selectedAspectRatioIndex = ratioIdx;
        OnPropertyChanged(nameof(SelectedAspectRatioIndex));
        OnPropertyChanged(nameof(AvailableResolutions));
        OnPropertyChanged(nameof(SelectedResolution));
    }

    private static string InferAspectRatio(int w, int h)
    {
        if (h == 0) return "16:9";
        var r = (double)w / h;
        if (r < 1.28) return "5:4";   // ~1.25
        if (r < 1.40) return "4:3";   // ~1.333
        if (r < 1.68) return "16:10"; // ~1.6
        return "16:9";                 // ~1.778+
    }

    private List<(string Label, int W, int H)> ResolutionsForCurrentRatio()
    {
        var ratio = AvailableAspectRatios[_selectedAspectRatioIndex];
        return AllResolutions.Where(r => r.Label == ratio).ToList();
    }

    public ResolutionItem[] AvailableResolutions =>
        ResolutionsForCurrentRatio()
            .Select(r => new ResolutionItem($"{r.W}×{r.H}", r.W, r.H, (r.W, r.H) == _monitorSize))
            .ToArray();

    public ResolutionItem? SelectedResolution
    {
        get
        {
            var list = ResolutionsForCurrentRatio();
            var s = _videoProvider.Get();
            var match = list.Find(r => r.W == s.Width && r.H == s.Height);
            var entry = match != default ? match : (list.Count > 0 ? list[0] : default);
            if (entry == default) return null;
            return new ResolutionItem($"{entry.W}×{entry.H}", entry.W, entry.H, (entry.W, entry.H) == _monitorSize);
        }
        set
        {
            if (value == null) return;
            var s = _videoProvider.Get();
            if (s.Width == value.W && s.Height == value.H) return;
            s.Width = value.W;
            s.Height = value.H;
            _videoProvider.Update(s);
            OnPropertyChanged();
        }
    }

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

    public void RefreshFromVideoProvider()
    {
        OnPropertyChanged(nameof(Fullscreen));
        OnPropertyChanged(nameof(NoWindowBorder));
        OnPropertyChanged(nameof(SelectedResolution));
    }

    // ── Constructor ────────────────────────────────────────────────────────────

    public GameSettingsViewModel(ICvarSettingsProvider cvarProvider, IVideoSettingsProvider videoProvider)
    {
        _cvarProvider = cvarProvider;
        _videoProvider = videoProvider;
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
