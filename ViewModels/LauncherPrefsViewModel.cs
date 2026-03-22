using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Models;
using d2c_launcher.Resources;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class LauncherPrefsViewModel : ViewModelBase
{
    private readonly ISettingsStorage _settingsStorage;

    // ── Game directory ─────────────────────────────────────────────────────────

    [ObservableProperty] private string _gameDirectory = Strings.NotSpecified;
    [ObservableProperty] private string _folderSizeText = "";

    public bool IsGameDirectorySet => !string.IsNullOrEmpty(_settingsStorage.Get().GameDirectory);

    public void RefreshGameDirectory()
    {
        var dir = _settingsStorage.Get().GameDirectory;
        GameDirectory = dir ?? Strings.NotSpecified;
        FolderSizeText = "";
        OnPropertyChanged(nameof(IsGameDirectorySet));

        // Refresh Vista compat state from registry — reads once per directory change
        // rather than on every binding evaluation.
        var exePath = string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "dota.exe");
        _vistaCompatEnabled = exePath != null && WindowsCompatibilityService.IsVistaCompatEnabled(exePath);
        OnPropertyChanged(nameof(VistaCompatibilityEnabled));

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
                    Dispatcher.UIThread.Post(() => FolderSizeText = text);
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

    // ── Launcher settings ─────────────────────────────────────────────────────

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

    // Cached so the getter doesn't perform a registry read on every binding evaluation.
    // Refreshed in RefreshGameDirectory() and updated in the setter on successful write.
    private bool _vistaCompatEnabled;

    public bool VistaCompatibilityEnabled
    {
        get => _vistaCompatEnabled;
        set
        {
            var dir = _settingsStorage.Get().GameDirectory;
            if (string.IsNullOrEmpty(dir))
            {
                OnPropertyChanged(); // snap back — no game dir, nothing to write
                return;
            }
            var exePath = Path.Combine(dir, "dota.exe");
            var success = WindowsCompatibilityService.SetVistaCompat(exePath, value);
            if (success)
                _vistaCompatEnabled = value;
            OnPropertyChanged();
        }
    }

    // ── Constructor ────────────────────────────────────────────────────────────

    public LauncherPrefsViewModel(ISettingsStorage settingsStorage)
    {
        _settingsStorage = settingsStorage;

        // Initialize Vista compat cache from registry at construction time.
        var dir = settingsStorage.Get().GameDirectory;
        if (!string.IsNullOrEmpty(dir))
        {
            var exePath = Path.Combine(dir, "dota.exe");
            _vistaCompatEnabled = WindowsCompatibilityService.IsVistaCompatEnabled(exePath);
        }
    }
}
