namespace d2c_launcher.Integration;

/// <summary>
/// Injectable abstraction over <see cref="DotaConsoleConnector"/>.
/// Allows consumers (e.g. <c>GameLaunchViewModel</c>) to be unit-tested without
/// P/Invoke calls that crash outside a Windows desktop environment.
/// </summary>
public interface IGameWindowService
{
    bool IsWindowOpen();
    void SetWindowIcon(string exePath);
    void FocusWindow();
}
