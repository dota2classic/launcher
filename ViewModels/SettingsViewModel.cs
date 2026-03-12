using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IGameLaunchSettingsStorage _launchStorage;
    private readonly ICvarSettingsProvider _cvarProvider;
    private readonly ISettingsStorage _settingsStorage;
    private readonly IVideoSettingsProvider _videoProvider;
    private readonly IContentRegistryService _registryService;

    /// <summary>Delegate to push a cvar change to a running game. Set by parent VM.</summary>
    public Action<string, string>? PushCvar { get; set; }

    /// <summary>
    /// Called when the user applies DLC changes. Receives the list of package IDs
    /// to remove (were installed, now unchecked). Parent VM uses this to trigger
    /// re-verification with file deletion.
    /// </summary>
    public Action<List<string>>? OnDlcChanged { get; set; }

    // ── DLC management ────────────────────────────────────────────────────────

    public IReadOnlyList<DlcPackageItem> DlcPackages { get; private set; } = [];

    /// <summary>Snapshot of id → installed state at the time DLC packages were loaded.</summary>
    private Dictionary<string, bool> _originalDlcSelection = new();

    [ObservableProperty] private bool _hasDlcChanges;

    public async Task LoadDlcPackagesAsync()
    {
        AppLog.Info("[DLC] LoadDlcPackagesAsync started");
        var registry = await _registryService.GetAsync();
        if (registry == null)
        {
            AppLog.Info("[DLC] Registry returned null — no packages to show");
            return;
        }
        AppLog.Info($"[DLC] Registry has {registry.Packages?.Count ?? 0} package(s)");

        var settings = _settingsStorage.Get();
        var installedIds = settings.InstalledPackageIds;
        var selectedDlcIds = settings.SelectedDlcIds ?? [];

        var items = new List<DlcPackageItem>();
        _originalDlcSelection = new Dictionary<string, bool>();

        foreach (var pkg in registry.Packages ?? [])
        {
            bool installed = installedIds != null
                ? installedIds.Contains(pkg.Id)
                : !pkg.Optional || selectedDlcIds.Contains(pkg.Id);

            var item = new DlcPackageItem
            {
                Id = pkg.Id,
                Name = pkg.Name,
                IsRequired = !pkg.Optional,
                IsSelected = installed
            };

            _originalDlcSelection[pkg.Id] = installed;

            if (pkg.Optional)
            {
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(DlcPackageItem.IsSelected))
                        UpdateHasDlcChanges();
                };
            }

            items.Add(item);
        }

        AppLog.Info($"[DLC] Built {items.Count} DlcPackageItem(s) for display");
        DlcPackages = items;
        HasDlcChanges = false;
        OnPropertyChanged(nameof(DlcPackages));
    }

    private void UpdateHasDlcChanges()
    {
        foreach (var item in DlcPackages)
        {
            if (_originalDlcSelection.TryGetValue(item.Id, out var original) && original != item.IsSelected)
            {
                HasDlcChanges = true;
                return;
            }
        }
        HasDlcChanges = false;
    }

    [RelayCommand]
    private void ApplyDlcChanges()
    {
        var removedIds = DlcPackages
            .Where(p => _originalDlcSelection.TryGetValue(p.Id, out var wasInstalled) && wasInstalled && !p.IsSelected)
            .Select(p => p.Id)
            .ToList();

        var addedIds = DlcPackages
            .Where(p => _originalDlcSelection.TryGetValue(p.Id, out var wasInstalled) && !wasInstalled && p.IsSelected)
            .Select(p => p.Id)
            .ToList();

        var settings = _settingsStorage.Get();

        // Update InstalledPackageIds
        if (settings.InstalledPackageIds != null)
        {
            foreach (var id in removedIds)
                settings.InstalledPackageIds.Remove(id);
            foreach (var id in addedIds)
                if (!settings.InstalledPackageIds.Contains(id))
                    settings.InstalledPackageIds.Add(id);
        }

        // Update SelectedDlcIds
        settings.SelectedDlcIds ??= [];
        foreach (var id in removedIds)
            settings.SelectedDlcIds.Remove(id);
        foreach (var id in addedIds)
            if (!settings.SelectedDlcIds.Contains(id))
                settings.SelectedDlcIds.Add(id);

        _settingsStorage.Save(settings);

        // Update snapshot so HasDlcChanges resets
        foreach (var item in DlcPackages)
            _originalDlcSelection[item.Id] = item.IsSelected;
        HasDlcChanges = false;

        OnDlcChanged?.Invoke(removedIds);
    }

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

    public string ExtraArgs
    {
        get => _launchStorage.Get().ExtraArgs ?? "";
        set
        {
            var s = _launchStorage.Get();
            var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (s.ExtraArgs == trimmed) return;
            s.ExtraArgs = trimmed;
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
                OnPropertyChanged(); // reset to current value
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

    public bool CloseToTray
    {
        get => _settingsStorage.Get().CloseToTray;
        set
        {
            var s = _settingsStorage.Get();
            if (s.CloseToTray == value) return;
            s.CloseToTray = value;
            _settingsStorage.Save(s);
            OnPropertyChanged();
        }
    }

    public bool AutoConnectToGame
    {
        get => _settingsStorage.Get().AutoConnectToGame;
        set
        {
            var s = _settingsStorage.Get();
            if (s.AutoConnectToGame == value) return;
            s.AutoConnectToGame = value;
            _settingsStorage.Save(s);
            OnPropertyChanged();
        }
    }

    public int UiScale
    {
        get => _settingsStorage.Get().UiScale;
        set
        {
            var clamped = Math.Clamp(value, 0, 4);
            var s = _settingsStorage.Get();
            if (s.UiScale == clamped) return;
            s.UiScale = clamped;
            _settingsStorage.Save(s);
            Services.UiScaleService.Apply(clamped);
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
        IVideoSettingsProvider videoProvider,
        IContentRegistryService registryService)
    {
        _launchStorage = launchStorage;
        _cvarProvider = cvarProvider;
        _settingsStorage = settingsStorage;
        _videoProvider = videoProvider;
        _registryService = registryService;
    }

    // Add new cvar property names here — RefreshFromCvarProvider picks them up automatically.
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

    /// <summary>
    /// Refreshes all UI-bound cvar properties.
    /// Called when config.cfg is re-read (e.g. after game exit).
    /// </summary>
    public void RefreshFromCvarProvider()
    {
        foreach (var name in CvarPropertyNames)
            OnPropertyChanged(name);
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
