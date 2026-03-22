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

    public void RefreshGameDirectory()
    {
        var dir = _settingsStorage.Get().GameDirectory;
        GameDirectory = dir ?? Strings.NotSpecified;
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

    public bool VistaCompatibilityEnabled
    {
        get
        {
            var dir = _settingsStorage.Get().GameDirectory;
            if (string.IsNullOrEmpty(dir)) return false;
            var exePath = System.IO.Path.Combine(dir, "dota.exe");
            return WindowsCompatibilityService.IsVistaCompatEnabled(exePath);
        }
        set
        {
            var dir = _settingsStorage.Get().GameDirectory;
            if (string.IsNullOrEmpty(dir)) return;
            var exePath = System.IO.Path.Combine(dir, "dota.exe");
            WindowsCompatibilityService.SetVistaCompat(exePath, value);
            OnPropertyChanged();
        }
    }

    // ── Constructor ────────────────────────────────────────────────────────────

    public LauncherPrefsViewModel(ISettingsStorage settingsStorage)
    {
        _settingsStorage = settingsStorage;
    }
}
