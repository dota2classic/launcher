using System.Runtime.Versioning;

namespace d2c_launcher.Integration;

/// <summary>
/// Injectable abstraction over <see cref="DotaConsoleConnector"/>.
/// Allows consumers (e.g. <c>GameLaunchViewModel</c>) to be unit-tested without
/// P/Invoke calls that crash outside a Windows desktop environment.
/// </summary>
[SupportedOSPlatform("windows")]
public interface IGameWindowService
{
    bool IsWindowOpen();
    void SetWindowIcon(string exePath);
    void FocusWindow();
}

/// <summary>
/// Default implementation — delegates to the static <see cref="DotaConsoleConnector"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public class GameWindowService : IGameWindowService
{
    public bool IsWindowOpen() => DotaConsoleConnector.IsWindowOpen();
    public void SetWindowIcon(string exePath) => DotaConsoleConnector.SetWindowIcon(exePath);
    public void FocusWindow() => DotaConsoleConnector.FocusWindow();
}
