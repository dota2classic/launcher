using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.ViewModels;

public partial class DlcPackageItem : ObservableObject
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>
    /// False for required packages and already-installed optional ones.
    /// The checkbox is shown but non-interactive.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    [ObservableProperty]
    private bool _isSelected;
}
