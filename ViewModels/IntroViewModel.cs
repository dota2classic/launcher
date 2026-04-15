using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

public partial class IntroViewModel : ObservableObject
{
    private readonly ISettingsStorage _settingsStorage;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private int _step = 1;

    public int StepCount => 4;

    public IntroViewModel(ISettingsStorage settingsStorage, bool isOpen)
    {
        _settingsStorage = settingsStorage;
        _isOpen = isOpen;
    }

    [RelayCommand]
    private void NextStep()
    {
        if (Step < StepCount)
            Step++;
        else
            Close();
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        var settings = _settingsStorage.Get();
        settings.IntroShown = true;
        _settingsStorage.Save(settings);
    }
}
