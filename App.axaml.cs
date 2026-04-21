using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
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
using Microsoft.Toolkit.Uwp.Notifications;

namespace d2c_launcher;

public partial class App : Application
{
    /// <summary>Set by Program.Main before Avalonia starts.</summary>
    public static Services.SingleInstanceService? SingleInstance { get; set; }

    /// <summary>Hardware ID computed at startup. Set by Program.Main before Avalonia starts.</summary>
    public static string Hwid { get; set; } = "unknown";

    private ServiceProvider? _services;
    private MainWindow? _mainWindow;

    private const string OpenLauncherArg = "d2c://game";

    private static void HandleActivationArgument(
        WindowService windowService,
        MainWindowViewModel mainVm,
        string? arg,
        bool restoreWindow)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return;

        if (restoreWindow)
            windowService.ShowAndActivate();

        if (arg.StartsWith("d2c://", StringComparison.OrdinalIgnoreCase))
            mainVm.HandleProtocolUrl(arg);
    }

    private static string? TryExtractProtocolArg(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        var args = message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Array.Find(args, a => a.StartsWith("d2c://", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldRestoreForProtocolArg(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return false;

        if (string.Equals(arg, OpenLauncherArg, StringComparison.OrdinalIgnoreCase))
            return true;

        return !arg.EndsWith("/decline", StringComparison.OrdinalIgnoreCase);
    }

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
                    AppLog.Error("[Unhandled] AppDomain exception", ex);
                FaroTelemetryService.ShutdownAsync().GetAwaiter().GetResult();
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                AppLog.Error("[Unhandled] Unobserved task exception", e.Exception.InnerException ?? e.Exception);
                e.SetObserved();
            };

            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                AppLog.Error("[Unhandled] UI thread exception", e.Exception);
                e.Handled = true;
                _ = FaroTelemetryService.FlushAsync();
            };

            var services = new ServiceCollection();
            services.AddSingleton<ICvarFileService, CvarFileService>();
            services.AddSingleton<IGameWindowService, GameWindowService>();
            services.AddSingleton<ISteamManager, SteamManager>();
            services.AddSingleton<ISettingsStorage, SettingsStorage>();
            services.AddSingleton<IGameLaunchSettingsStorage, GameLaunchSettingsStorage>();
            services.AddSingleton<ICvarSettingsProvider, CvarSettingsProvider>();
            services.AddSingleton<IVideoSettingsProvider, VideoSettingsProvider>();
            services.AddSingleton<ISteamAuthApi, SteamAuthApi>();
            services.AddSingleton<IBackendApiService, BackendApiService>();
            services.AddSingleton<IHttpImageService, HttpImageService>();
            services.AddSingleton<IEmoticonService, EmoticonService>();
            services.AddSingleton<IUiDispatcher, AvaloniaDispatcher>();
            services.AddSingleton<ISocketFactory, RealSocketFactory>();
            services.AddSingleton<IQueueSocketService, QueueSocketService>();
            services.AddSingleton<UpdateService>();
            services.AddSingleton<ILocalManifestService, LocalManifestService>();
            services.AddSingleton<IManifestDiffService, ManifestDiffService>();
            services.AddSingleton<IGameDownloadService, GameDownloadService>();
            services.AddSingleton<RedistInstallService>();
            services.AddSingleton<IContentRegistryService, ContentRegistryService>();
            services.AddSingleton<ITriviaRepository, LocalJsonTriviaRepository>();
            services.AddSingleton<ITimerFactory, AvaloniaTimerFactory>();
            services.AddSingleton<IUserNameResolver, UserNameResolver>();
            services.AddSingleton<IEmoticonSnapshotBuilder, EmoticonSnapshotBuilder>();
            services.AddSingleton<IChatViewModelFactory, ChatViewModelFactory>();
            services.AddSingleton<IWindowService, WindowService>();
            services.AddSingleton<IToastNotificationService, ToastNotificationService>(
                _ => new ToastNotificationService());
            services.AddSingleton<INetConService, NetConService>();
            services.AddSingleton<IDotakeysProfileService, DotakeysProfileService>();
            services.AddSingleton<MainWindowViewModel>();

            _services = services.BuildServiceProvider();

            ProtocolRegistrationService.EnsureRegistered();

            var steamManager = _services.GetRequiredService<ISteamManager>();
            var queueSocket = _services.GetRequiredService<IQueueSocketService>();
            var mainVm = _services.GetRequiredService<MainWindowViewModel>();
            var windowService = (WindowService)_services.GetRequiredService<IWindowService>();

            var settingsStorage = _services.GetRequiredService<ISettingsStorage>();
            Services.UiScaleService.Apply(settingsStorage.Get().UiScale);
            _mainWindow = new MainWindow(settingsStorage) { DataContext = mainVm };
            windowService.SetWindow(_mainWindow);

            // Handle protocol URL passed as initial arg (e.g. launched via d2c:// link).
            var protocolArg = Array.Find(args, a => a.StartsWith("d2c://", StringComparison.OrdinalIgnoreCase));
            if (protocolArg != null)
                HandleActivationArgument(windowService, mainVm, protocolArg, restoreWindow: true);

            // All external activations (toast callback, startup args, second-instance forwarding)
            // are normalized to an activation argument and routed through the same handler.
            ToastNotificationManagerCompat.OnActivated += e =>
            {
                var launchArg = e.Argument;
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    HandleActivationArgument(
                        windowService,
                        mainVm,
                        launchArg,
                        restoreWindow: ShouldRestoreForProtocolArg(launchArg));
                });
            };

            // Handle messages forwarded from second instances.
            // "__show__" means a second launch with no args — restore from tray.
            // d2c:// URLs are extracted from the forwarded argv payload and routed the same
            // way as in-process toast activations.
            if (SingleInstance != null)
            {
                SingleInstance.OnMessageReceived += msg =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (msg == "__show__")
                        {
                            windowService.ShowAndActivate();
                            return;
                        }

                        var forwardedProtocolArg = TryExtractProtocolArg(msg);
                        if (forwardedProtocolArg != null)
                        {
                            var isToastActivation = msg.Contains("-ToastActivated", StringComparison.OrdinalIgnoreCase);
                            var shouldRestore = !isToastActivation || ShouldRestoreForProtocolArg(forwardedProtocolArg);
                            HandleActivationArgument(windowService, mainVm, forwardedProtocolArg, shouldRestore);
                            return;
                        }

                        if (msg.Contains("-ToastActivated", StringComparison.OrdinalIgnoreCase))
                            windowService.ShowAndActivate();
                    });
                };
            }

            desktop.Exit += (_, _) =>
            {
                steamManager.Dispose();
                queueSocket.Dispose();
                _services.Dispose();
                SingleInstance?.Dispose();
            };

            desktop.MainWindow = _mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void TrayOpen_Click(object? sender, EventArgs e)
    {
        _mainWindow?.ShowAndActivate();
    }

    private void TrayExit_Click(object? sender, EventArgs e)
    {
        if (_mainWindow != null)
            _mainWindow.RealExit = true;
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
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
