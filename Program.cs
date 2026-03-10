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
        // Disable Velopack's "auto-apply on startup" (it is ON by default).
        // We download updates silently but only apply them when the user clicks
        // "Restart and update" in the launcher UI.
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();

        // Skip single-instance enforcement in preview mode so the preview tool
        // can run alongside a running launcher instance.
        var isPreview = Array.IndexOf(args, "--preview") >= 0;

        // Enforce single instance: if another launcher is already running, forward
        // our args to it (e.g. a d2c:// protocol URL) and exit immediately.
        var singleInstance = new SingleInstanceService();
        if (!isPreview && !singleInstance.TryBecomePrimaryInstance(args))
        {
            singleInstance.Dispose();
            return;
        }

        // We are the primary instance — start the pipe server before Avalonia so
        // it is ready to receive messages from any second instance that starts
        // while we are initialising.
        singleInstance.StartPipeServer();

        // Expose the service so App can wire protocol handling after DI is set up.
        App.SingleInstance = singleInstance;

        var hw = HardwareInfoService.Collect();
        var asm = Assembly.GetExecutingAssembly();
        // AssemblyInformationalVersion keeps the full semver (e.g. "0.0.97-pre+abc123").
        // AssemblyVersion is numeric-only (e.g. "0.0.97.0"), losing the pre-release suffix.
        var version = (asm.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
                          ?.InformationalVersion
                          ?.Split('+')[0])           // strip build metadata hash
                      ?? asm.GetName().Version?.ToString(3)
                      ?? "0.0.0";
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
