using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.Models;

public sealed partial class MatchmakingModeView : ObservableObject
{
    public int ModeId { get; }
    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _inQueue;

    [ObservableProperty]
    private string? _restrictionText;

    public bool IsRestricted => !string.IsNullOrEmpty(RestrictionText);

    partial void OnRestrictionTextChanged(string? value) => OnPropertyChanged(nameof(IsRestricted));

    public MatchmakingModeView(int modeId, string name, bool isSelected = false)
    {
        ModeId = modeId;
        Name = name;
        _isSelected = isSelected;
    }
}
