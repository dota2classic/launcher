using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using d2c_launcher.Integration;
using d2c_launcher.Util;
using d2c_launcher.Preview;
using d2c_launcher.Services;
using d2c_launcher.ViewModels;
using d2c_launcher.Views;
using Microsoft.Extensions.DependencyInjection;

namespace d2c_launcher;

public partial class App : Application
{
    /// <summary>Set by Program.Main before Avalonia starts.</summary>
    public static Services.SingleInstanceService? SingleInstance { get; set; }

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

            var args = desktop.Args ?? [];
            var previewIdx = Array.IndexOf(args, "--preview");
            if (previewIdx >= 0)
            {
                var componentName = previewIdx + 1 < args.Length ? args[previewIdx + 1] : "";
                desktop.MainWindow = new PreviewWindow(componentName);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    FaroTelemetryService.TrackException(ex);
                FaroTelemetryService.ShutdownAsync().GetAwaiter().GetResult();
            };

            var services = new ServiceCollection();
            services.AddSingleton<SteamManager>();
            services.AddSingleton<ISettingsStorage, SettingsStorage>();
            services.AddSingleton<IGameLaunchSettingsStorage, GameLaunchSettingsStorage>();
            services.AddSingleton<ICvarSettingsProvider, CvarSettingsProvider>();
            services.AddSingleton<IVideoSettingsProvider, VideoSettingsProvider>();
            services.AddSingleton<ISteamAuthApi, SteamAuthApi>();
            services.AddSingleton<IBackendApiService, BackendApiService>();
            services.AddSingleton<IHttpImageService, HttpImageService>();
            services.AddSingleton<IEmoticonService, EmoticonService>();
            services.AddSingleton<IQueueSocketService, QueueSocketService>();
            services.AddSingleton<UpdateService>();
            services.AddSingleton<ILocalManifestService, LocalManifestService>();
            services.AddSingleton<IManifestDiffService, ManifestDiffService>();
            services.AddSingleton<IGameDownloadService, GameDownloadService>();
            services.AddSingleton<RedistInstallService>();
            services.AddSingleton<IContentRegistryService, ContentRegistryService>();
            services.AddSingleton<MainWindowViewModel>();

            _services = services.BuildServiceProvider();

            ProtocolRegistrationService.EnsureRegistered();

            var steamManager = _services.GetRequiredService<SteamManager>();
            var queueSocket = _services.GetRequiredService<IQueueSocketService>();
            var mainVm = _services.GetRequiredService<MainWindowViewModel>();

            // Handle protocol URL passed as initial arg (e.g. launched via d2c:// link).
            var protocolArg = Array.Find(args, a => a.StartsWith("d2c://", StringComparison.OrdinalIgnoreCase));
            if (protocolArg != null)
                mainVm.HandleProtocolUrl(protocolArg);

            // Handle protocol URLs forwarded from second instances that started while we were running.
            if (SingleInstance != null)
            {
                SingleInstance.OnMessageReceived += msg =>
                    mainVm.HandleProtocolUrl(msg);
            }

            desktop.Exit += (_, _) =>
            {
                steamManager.Dispose();
                queueSocket.Dispose();
                _services.Dispose();
                SingleInstance?.Dispose();
            };

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
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
