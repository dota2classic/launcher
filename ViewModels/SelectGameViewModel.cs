using System;

namespace d2c_launcher.ViewModels;

public class SelectGameViewModel : ViewModelBase
{
    internal Action<string>? GameDirectorySelected { get; set; }

    internal void NotifyGameDirectorySelected(string path)
    {
        GameDirectorySelected?.Invoke(path);
    }
}
