using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

/// <summary>
/// Thin container that composes the three focused settings sub-ViewModels.
/// External callers (MainLauncherViewModel, SettingsPanel) interact with this
/// type; each sub-VM owns a single concern.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    public GameSettingsViewModel GameSettings { get; }
    public LauncherPrefsViewModel LauncherPrefs { get; }
    public DlcViewModel Dlc { get; }

    public Action<string, string>? PushCvar
    {
        get => GameSettings.PushCvar;
        set => GameSettings.PushCvar = value;
    }

    public Action<List<string>>? OnDlcChanged
    {
        get => Dlc.OnDlcChanged;
        set => Dlc.OnDlcChanged = value;
    }

    public void RefreshFromCvarProvider() => GameSettings.RefreshFromCvarProvider();
    public void RefreshFromVideoProvider() => GameSettings.RefreshFromVideoProvider();
    public void RefreshGameDirectory() => LauncherPrefs.RefreshGameDirectory();
    public Task LoadDlcPackagesAsync() => Dlc.LoadDlcPackagesAsync();

    public SettingsViewModel(
        IGameLaunchSettingsStorage launchStorage,
        ICvarSettingsProvider cvarProvider,
        ISettingsStorage settingsStorage,
        IVideoSettingsProvider videoProvider,
        IContentRegistryService registryService)
    {
        GameSettings = new GameSettingsViewModel(cvarProvider, videoProvider);
        LauncherPrefs = new LauncherPrefsViewModel(launchStorage, settingsStorage);
        Dlc = new DlcViewModel(settingsStorage, registryService);
    }
}
