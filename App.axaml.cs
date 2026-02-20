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
using Microsoft.Extensions.DependencyInjection;

namespace d2c_launcher;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            var services = new ServiceCollection();
            services.AddSingleton<SteamManager>();
            services.AddSingleton<ISettingsStorage, SettingsStorage>();
            services.AddSingleton<ISteamAuthApi, SteamAuthApi>();
            services.AddSingleton<IBackendApiService, BackendApiService>();
            services.AddSingleton<IQueueSocketService, QueueSocketService>();
            services.AddTransient<MainWindowViewModel>();

            _services = services.BuildServiceProvider();

            var steamManager = _services.GetRequiredService<SteamManager>();
            var queueSocket = _services.GetRequiredService<IQueueSocketService>();
            desktop.Exit += (_, _) =>
            {
                steamManager.Dispose();
                queueSocket.Dispose();
                _services.Dispose();
            };

            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>(),
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
