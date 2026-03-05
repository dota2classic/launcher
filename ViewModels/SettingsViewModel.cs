using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Models;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IGameLaunchSettingsStorage _launchStorage;
    private readonly ICvarSettingsProvider _cvarProvider;
    private readonly ISettingsStorage _settingsStorage;
    private readonly IVideoSettingsProvider _videoProvider;

    /// <summary>Delegate to push a cvar change to a running game. Set by parent VM.</summary>
    public Action<string, string>? PushCvar { get; set; }

    // ── Game directory ────────────────────────────────────────────────────────

    [ObservableProperty] private string _gameDirectory = "Не указано";
    [ObservableProperty] private string _folderSizeText = "";

    public void RefreshGameDirectory()
    {
        var dir = _settingsStorage.Get().GameDirectory;
        GameDirectory = dir ?? "Не указано";
        FolderSizeText = "";

        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            _ = Task.Run(() =>
            {
                try
                {
                    var bytes = ComputeFolderSize(dir);
                    var gb = bytes / (1024.0 * 1024 * 1024);
                    var text = gb >= 1
                        ? $"{gb:F1} ГБ"
                        : $"{bytes / (1024.0 * 1024):F0} МБ";
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => FolderSizeText = text);
                }
                catch
                {
                    // ignore — folder may be inaccessible
                }
            });
        }
    }

    private static long ComputeFolderSize(string dir)
    {
        long total = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { total += new FileInfo(file).Length; }
            catch { /* skip locked/inaccessible files */ }
        }
        return total;
    }

    // ── Launch parameters ──────────────────────────────────────────────────────

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

    public static string[] AvailableResolutions { get; } =
        ["1280×720", "1366×768", "1280×1024", "1600×900", "1920×1080", "2560×1440"];

    private static readonly (int W, int H)[] ResolutionValues =
        [(1280, 720), (1366, 768), (1280, 1024), (1600, 900), (1920, 1080), (2560, 1440)];

    public int SelectedResolutionIndex
    {
        get
        {
            var s = _videoProvider.Get();
            for (var i = 0; i < ResolutionValues.Length; i++)
                if (ResolutionValues[i].W == s.Width && ResolutionValues[i].H == s.Height)
                    return i;
            return 4; // default 1920×1080
        }
        set
        {
            if (value < 0 || value >= ResolutionValues.Length) return;
            var s = _videoProvider.Get();
            var (w, h) = ResolutionValues[value];
            if (s.Width == w && s.Height == h) return;
            s.Width = w;
            s.Height = h;
            _videoProvider.Update(s);
            OnPropertyChanged();
        }
    }

    // ── Gameplay (cvars backed by config.cfg) ─────────────────────────────────

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

    public bool QuickCast
    {
        get => _cvarProvider.Get().QuickCast;
        set
        {
            var s = _cvarProvider.Get();
            if (s.QuickCast == value) return;
            s.QuickCast = value;
            _cvarProvider.Update(s);
            OnPropertyChanged();
            PushCvar?.Invoke("dota_quick_select_setting", value ? "1" : "0");
        }
    }

    // ── Auto-attack mode ───────────────────────────────────────────────────────

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

    // ── Launcher settings ─────────────────────────────────────────────────────

    public bool AutoUpdate
    {
        get => _settingsStorage.Get().AutoUpdate;
        set
        {
            var s = _settingsStorage.Get();
            if (s.AutoUpdate == value) return;
            s.AutoUpdate = value;
            _settingsStorage.Save(s);
            OnPropertyChanged();
        }
    }

    public bool DefenderExclusionEnabled
    {
        get
        {
            var s = _settingsStorage.Get();
            return !string.IsNullOrEmpty(s.GameDirectory)
                && s.DefenderExclusionPath == s.GameDirectory;
        }
        set => _ = SetDefenderExclusionAsync(value);
    }

    private async Task SetDefenderExclusionAsync(bool enabled)
    {
        var s = _settingsStorage.Get();
        var dir = s.GameDirectory;
        if (string.IsNullOrEmpty(dir)) return;

        if (enabled)
        {
            await WindowsDefenderService.TryAddExclusionAsync(dir);
            s = _settingsStorage.Get();
            s.DefenderExclusionPath = dir;
            _settingsStorage.Save(s);
        }
        else
        {
            await WindowsDefenderService.TryRemoveExclusionAsync(dir);
            s = _settingsStorage.Get();
            s.DefenderExclusionPath = null;
            _settingsStorage.Save(s);
        }

        Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(DefenderExclusionEnabled)));
    }

    // ── Constructor ────────────────────────────────────────────────────────────

    public SettingsViewModel(
        IGameLaunchSettingsStorage launchStorage,
        ICvarSettingsProvider cvarProvider,
        ISettingsStorage settingsStorage,
        IVideoSettingsProvider videoProvider)
    {
        _launchStorage = launchStorage;
        _cvarProvider = cvarProvider;
        _settingsStorage = settingsStorage;
        _videoProvider = videoProvider;
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
        OnPropertyChanged(nameof(QuickCast));
    }

    /// <summary>
    /// Refreshes all UI-bound video properties.
    /// Called when video.txt is re-read (e.g. after game exit).
    /// </summary>
    public void RefreshFromVideoProvider()
    {
        OnPropertyChanged(nameof(Fullscreen));
        OnPropertyChanged(nameof(NoWindowBorder));
        OnPropertyChanged(nameof(SelectedResolutionIndex));
    }
}
