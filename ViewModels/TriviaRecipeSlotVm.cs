using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.ViewModels;

public partial class TriviaRecipeSlotVm : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilled))]
    private string? _filledImageUri;

    public bool IsFilled => FilledImageUri != null;
}
