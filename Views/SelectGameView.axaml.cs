using System.IO;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Views;

public partial class SelectGameView : UserControl
{
    public SelectGameView()
    {
        InitializeComponent();
    }

    private async void OnBrowseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = this.GetVisualRoot() as TopLevel;
        if (topLevel?.StorageProvider == null || DataContext is not SelectGameViewModel vm)
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
            var dir = string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                vm.NotifyGameDirectorySelected(dir);
        }
    }
}
