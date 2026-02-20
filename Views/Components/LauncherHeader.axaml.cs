using System.IO;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Views.Components;

public partial class LauncherHeader : UserControl
{
    public LauncherHeader()
    {
        InitializeComponent();
    }

    private async void OnSelectDotaExeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = this.GetVisualRoot() as TopLevel;
        if (topLevel?.StorageProvider == null || DataContext is not MainLauncherViewModel vm)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select dota.exe",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Executable") { Patterns = new[] { "*.exe" } },
                FilePickerFileTypes.All
            }
        });

        if (files.Count > 0)
        {
            var path = files[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
                vm.SetGameDirectory(Path.GetDirectoryName(path));
        }
    }

    private void OnPrimaryActionClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainLauncherViewModel vm)
            return;

        if (!vm.IsGameDirectorySet)
        {
            OnSelectDotaExeClicked(sender, e);
            return;
        }

        vm.LaunchGame();
    }

    private void OnSettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.OpenSettings();
    }
}
