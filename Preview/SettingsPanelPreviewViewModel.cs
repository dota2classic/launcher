using d2c_launcher.Services;

namespace d2c_launcher.Preview;

public class SettingsPanelPreviewViewModel
{
    private readonly IGameLaunchSettingsStorage _storage;

    public static string[] AvailableLanguages { get; } = ["russian", "english"];

    public string SelectedLanguage
    {
        get => _storage.Get().Language;
        set
        {
            var s = _storage.Get();
            s.Language = value;
            _storage.Save(s);
        }
    }

    public bool NoVid
    {
        get => _storage.Get().NoVid;
        set
        {
            var s = _storage.Get();
            s.NoVid = value;
            _storage.Save(s);
        }
    }

    public bool ColorblindMode
    {
        get => _storage.Get().ColorblindMode;
        set
        {
            var s = _storage.Get();
            s.ColorblindMode = value;
            _storage.Save(s);
        }
    }

    public SettingsPanelPreviewViewModel(IGameLaunchSettingsStorage storage)
    {
        _storage = storage;
    }
}
