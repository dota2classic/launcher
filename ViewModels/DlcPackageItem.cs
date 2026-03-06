using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.ViewModels;

public partial class DlcPackageItem : ObservableObject
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>True for required (non-optional) packages — checkbox locked checked.</summary>
    public bool IsRequired { get; init; }

    /// <summary>Whether the user can toggle this package. False for required packages.</summary>
    public bool IsEnabled => !IsRequired;

    [ObservableProperty]
    private bool _isSelected;
}
