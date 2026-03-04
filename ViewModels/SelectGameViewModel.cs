using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.ViewModels;

public partial class SelectGameViewModel : ViewModelBase
{
    [ObservableProperty] private string? _selectedDownloadPath;
    [ObservableProperty] private string? _selectedInstalledPath;

    internal Action<string>? GameDirectorySelected { get; set; }

    internal void NotifyGameDirectorySelected(string path)
    {
        GameDirectorySelected?.Invoke(path);
    }
}
