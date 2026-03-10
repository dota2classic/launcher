using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using d2c_launcher.Services;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Views;

public partial class SelectGameView : UserControl
{
    public SelectGameView()
    {
        InitializeComponent();
    }

    private async void OnDownloadClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await PickFolderAsync();

    private async void OnAlreadyInstalledClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await PickInstalledExeAsync();

    private async void OnChangeDownloadClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SelectGameViewModel vm)
            vm.SelectedDownloadPath = null;
        await PickFolderAsync();
    }

    private async void OnChangeInstalledClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SelectGameViewModel vm)
        {
            vm.SelectedInstalledPath = null;
            vm.InstalledPathError = null;
        }
        await PickInstalledExeAsync();
    }

    private async Task PickFolderAsync()
    {
        var topLevel = this.GetVisualRoot() as TopLevel;
        if (topLevel?.StorageProvider == null || DataContext is not SelectGameViewModel vm)
            return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите папку для установки Dotaclassic",
            AllowMultiple = false
        });

        if (folders.Count == 0)
            return;

        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
            return;

        var gameDir = Path.Combine(path, "dotaclassic684");
        Directory.CreateDirectory(gameDir);
        vm.SelectedDownloadPath = gameDir;
        await vm.StartDlcSelectionAsync(gameDir);
    }

    private async Task PickInstalledExeAsync()
    {
        var topLevel = this.GetVisualRoot() as TopLevel;
        if (topLevel?.StorageProvider == null || DataContext is not SelectGameViewModel vm)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите dota.exe",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("dota.exe") { Patterns = new[] { "dota.exe" } }
            }
        });

        if (files.Count == 0)
            return;

        var exePath = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(exePath))
            return;

        var dir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(dir))
            return;

        if (!GameDirectoryValidator.IsAcceptable(dir, out var validationError))
        {
            vm.InstalledPathError = validationError;
            return;
        }

        vm.InstalledPathError = null;
        vm.SelectedInstalledPath = dir;
        vm.NotifyGameDirectorySelected(dir);
    }
}
