using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using d2c_launcher.Integration;
using d2c_launcher.Services;
using d2c_launcher.ViewModels;
using d2c_launcher.Views;

namespace d2c_launcher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            var steamManager = new SteamManager();
            desktop.Exit += (_, _) => steamManager.Dispose();
            var settingsStorage = new SettingsStorage();
            var steamAuthApi = new SteamAuthApi();
            var backendApi = new BackendApiService();
            var queueSocket = new QueueSocketService();
            desktop.Exit += (_, _) => queueSocket.Dispose();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(steamManager, settingsStorage, steamAuthApi, backendApi, queueSocket),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
