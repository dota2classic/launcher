using System.Runtime.Versioning;

namespace d2c_launcher.Integration;

/// <summary>
/// Default implementation of <see cref="IGameWindowService"/> — delegates to the static
/// <see cref="DotaConsoleConnector"/> used in production.
/// </summary>
[SupportedOSPlatform("windows")]
public class GameWindowService : IGameWindowService
{
    public bool IsWindowOpen() => DotaConsoleConnector.IsWindowOpen();
    public void SetWindowIcon(string exePath) => DotaConsoleConnector.SetWindowIcon(exePath);
    public void FocusWindow() => DotaConsoleConnector.FocusWindow();
}
