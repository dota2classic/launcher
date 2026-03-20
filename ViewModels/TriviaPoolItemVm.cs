using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.ViewModels;

public partial class TriviaPoolItemVm : ObservableObject
{
    public string ItemKey { get; init; } = "";
    public string ImageUri { get; init; } = "";
    internal bool IsCorrect { get; init; }

    [ObservableProperty] private bool _isUsed;
}
