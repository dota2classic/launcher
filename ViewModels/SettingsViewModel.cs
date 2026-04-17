using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

/// <summary>
/// Thin container that composes the focused settings sub-ViewModels.
/// External callers (MainLauncherViewModel, SettingsPanel) interact with this
/// type; each sub-VM owns a single concern matching its view.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    public VideoSettingsViewModel VideoSettings { get; }
    public GameplayViewModel Gameplay { get; }
    public LauncherPrefsViewModel LauncherPrefs { get; }
    public DlcViewModel Dlc { get; }

    public Action<string, string>? PushCvar
    {
        get => Gameplay.PushCvar;
        set => Gameplay.PushCvar = value;
    }

    public Action? OnDlcChanged
    {
        get => Dlc.OnDlcChanged;
        set => Dlc.OnDlcChanged = value;
    }

    public void RefreshFromCvarProvider() => Gameplay.RefreshFromCvarProvider();
    public void RefreshFromVideoProvider() => VideoSettings.RefreshFromVideoProvider();
    public void RefreshGameDirectory() => LauncherPrefs.RefreshGameDirectory();
    public Task LoadDlcPackagesAsync() => Dlc.LoadDlcPackagesAsync();

    public SettingsViewModel(
        IGameLaunchSettingsStorage launchStorage,
        ICvarSettingsProvider cvarProvider,
        ISettingsStorage settingsStorage,
        IVideoSettingsProvider videoProvider,
        IContentRegistryService registryService)
    {
        VideoSettings = new VideoSettingsViewModel(videoProvider, launchStorage);
        Gameplay = new GameplayViewModel(cvarProvider);
        LauncherPrefs = new LauncherPrefsViewModel(settingsStorage);
        Dlc = new DlcViewModel(settingsStorage, registryService);
    }
}
