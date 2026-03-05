using Avalonia;
using System;
using System.Reflection;
using d2c_launcher.Services;
using Velopack;

namespace d2c_launcher;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        var hw = HardwareInfoService.Collect();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        FaroTelemetryService.Init(version, hw);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        FaroTelemetryService.ShutdownAsync().GetAwaiter().GetResult();
        // Force-exit in case any third-party library (e.g. SocketIOClient) left foreground
        // threads running, which would otherwise keep the process alive indefinitely.
        Environment.Exit(0);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
