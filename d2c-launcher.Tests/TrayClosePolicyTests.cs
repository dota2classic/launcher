using d2c_launcher.Models;
using d2c_launcher.Views;
using Xunit;

namespace d2c_launcher.Tests;

public class TrayClosePolicyTests
{
    // Happy path: all conditions met → hide to tray
    [Fact]
    public void ShouldHideToTray_WhenAllConditionsMet()
    {
        Assert.True(TrayClosePolicy.ShouldHideToTray(
            realExit: false,
            closeToTray: true,
            isUserClose: true,
            appState: AppState.Launcher));
    }

    // RealExit flag overrides everything
    [Fact]
    public void ShouldNotHide_WhenRealExitIsSet()
    {
        Assert.False(TrayClosePolicy.ShouldHideToTray(
            realExit: true,
            closeToTray: true,
            isUserClose: true,
            appState: AppState.Launcher));
    }

    // Setting disabled
    [Fact]
    public void ShouldNotHide_WhenCloseToTrayDisabled()
    {
        Assert.False(TrayClosePolicy.ShouldHideToTray(
            realExit: false,
            closeToTray: false,
            isUserClose: true,
            appState: AppState.Launcher));
    }

    // Programmatic close (e.g. update / OS shutdown)
    [Fact]
    public void ShouldNotHide_WhenNotUserClose()
    {
        Assert.False(TrayClosePolicy.ShouldHideToTray(
            realExit: false,
            closeToTray: true,
            isUserClose: false,
            appState: AppState.Launcher));
    }

    // Not fully initialized — the core fix for issue #51
    [Theory]
    [InlineData(AppState.CheckingSteam)]
    [InlineData(AppState.SteamNotRunning)]
    [InlineData(AppState.SteamOffline)]
    [InlineData(AppState.SelectGameDirectory)]
    [InlineData(AppState.VerifyingGame)]
    public void ShouldNotHide_WhenNotInLauncherState(AppState state)
    {
        Assert.False(TrayClosePolicy.ShouldHideToTray(
            realExit: false,
            closeToTray: true,
            isUserClose: true,
            appState: state));
    }
}
