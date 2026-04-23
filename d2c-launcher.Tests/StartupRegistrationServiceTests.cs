using d2c_launcher.Models;
using d2c_launcher.Services;
using Xunit;

namespace d2c_launcher.Tests;

public class StartupRegistrationServiceTests
{
    [Fact]
    public void AutoLaunchOnStartup_DefaultsToEnabled()
    {
        var settings = new LauncherSettings();

        Assert.True(settings.AutoLaunchOnStartup);
    }

    [Fact]
    public void BuildRunCommand_UsesQuotedExePathAndBackgroundFlag()
    {
        var command = StartupRegistrationService.BuildRunCommand(@"C:\Program Files\d2c\d2c-launcher.exe");

        Assert.Equal(@"""C:\Program Files\d2c\d2c-launcher.exe"" --background-start", command);
    }
}
