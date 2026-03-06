using d2c_launcher.Models;
using Xunit;

namespace d2c_launcher.Tests;

/// <summary>
/// Tests for the Windows Defender exclusion prompt decision logic
/// (<see cref="LauncherSettings.ShouldShowDefenderPrompt"/>).
/// </summary>
public class DefenderPromptTests
{
    [Fact]
    public void FreshInstall_ShowsPrompt()
    {
        var s = new LauncherSettings();
        // DefenderPromptAnswered defaults false, DefenderExclusionPath defaults null
        Assert.True(s.ShouldShowDefenderPrompt);
    }

    [Fact]
    public void UserAccepted_DoesNotShowPrompt()
    {
        var s = new LauncherSettings
        {
            DefenderPromptAnswered = true,
            DefenderExclusionPath = @"C:\Games\Dota2Classic",
        };
        Assert.False(s.ShouldShowDefenderPrompt);
    }

    [Fact]
    public void UserDeclined_DoesNotShowPrompt()
    {
        // User clicked "No thanks" — answered but no path recorded
        var s = new LauncherSettings
        {
            DefenderPromptAnswered = true,
            DefenderExclusionPath = null,
        };
        Assert.False(s.ShouldShowDefenderPrompt);
    }

    [Fact]
    public void LegacyUser_ExclusionPathSetBeforeFieldExisted_DoesNotShowPrompt()
    {
        // Before DefenderPromptAnswered was introduced, only DefenderExclusionPath was saved.
        // Such users should never see the prompt again.
        var s = new LauncherSettings
        {
            DefenderPromptAnswered = false,
            DefenderExclusionPath = @"C:\Games\Dota2Classic",
        };
        Assert.False(s.ShouldShowDefenderPrompt);
    }
}
