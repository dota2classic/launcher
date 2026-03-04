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

    private async void OnDownloadClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await PickFolderAsync();

    private async void OnAlreadyInstalledClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await PickFolderAsync();

    private async System.Threading.Tasks.Task PickFolderAsync()
    {
        var topLevel = this.GetVisualRoot() as TopLevel;
        if (topLevel?.StorageProvider == null || DataContext is not SelectGameViewModel vm)
            return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите папку Dota 2 Classic",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var path = folders[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
                vm.NotifyGameDirectorySelected(path);
        }
    }
}
