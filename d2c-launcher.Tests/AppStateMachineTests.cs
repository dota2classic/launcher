using d2c_launcher.Models;
using d2c_launcher.ViewModels;
using Xunit;

namespace d2c_launcher.Tests;

/// <summary>
/// Unit tests for <see cref="AppStateMachine"/>.
/// All tests are pure — no UI, no services, no async.
/// </summary>
public class AppStateMachineTests
{
    // ── OnSteamUpdate: steam stops ────────────────────────────────────────────

    [Theory]
    [InlineData(AppState.CheckingSteam)]
    [InlineData(AppState.SteamNotRunning)]
    [InlineData(AppState.SteamOffline)]
    [InlineData(AppState.SelectGameDirectory)]
    [InlineData(AppState.VerifyingGame)]
    [InlineData(AppState.Launcher)]
    public void SteamNotRunning_FromAnyState_TransitionsToSteamNotRunning(AppState current)
    {
        var next = AppStateMachine.OnSteamUpdate(current, SteamStatus.NotRunning, hasUser: true, hasGameDir: true);
        Assert.Equal(AppState.SteamNotRunning, next);
    }

    [Theory]
    [InlineData(AppState.CheckingSteam)]
    [InlineData(AppState.SteamNotRunning)]
    [InlineData(AppState.SteamOffline)]
    [InlineData(AppState.SelectGameDirectory)]
    [InlineData(AppState.VerifyingGame)]
    [InlineData(AppState.Launcher)]
    public void SteamOffline_FromAnyState_TransitionsToSteamOffline(AppState current)
    {
        var next = AppStateMachine.OnSteamUpdate(current, SteamStatus.Offline, hasUser: false, hasGameDir: true);
        Assert.Equal(AppState.SteamOffline, next);
    }

    // ── OnSteamUpdate: still checking ────────────────────────────────────────

    [Fact]
    public void SteamChecking_TransitionsToCheckingSteam()
    {
        var next = AppStateMachine.OnSteamUpdate(AppState.SteamNotRunning, SteamStatus.Checking, hasUser: false, hasGameDir: false);
        Assert.Equal(AppState.CheckingSteam, next);
    }

    [Fact]
    public void SteamRunning_NoUser_TransitionsToCheckingSteam()
    {
        var next = AppStateMachine.OnSteamUpdate(AppState.SteamNotRunning, SteamStatus.Running, hasUser: false, hasGameDir: true);
        Assert.Equal(AppState.CheckingSteam, next);
    }

    // ── OnSteamUpdate: steam ready, routing by game dir ──────────────────────

    [Fact]
    public void SteamReady_NoGameDir_TransitionsToSelectGameDirectory()
    {
        var next = AppStateMachine.OnSteamUpdate(AppState.CheckingSteam, SteamStatus.Running, hasUser: true, hasGameDir: false);
        Assert.Equal(AppState.SelectGameDirectory, next);
    }

    [Fact]
    public void SteamReady_WithGameDir_TransitionsToVerifyingGame()
    {
        var next = AppStateMachine.OnSteamUpdate(AppState.CheckingSteam, SteamStatus.Running, hasUser: true, hasGameDir: true);
        Assert.Equal(AppState.VerifyingGame, next);
    }

    [Fact]
    public void SteamReady_FromSteamOffline_NoGameDir_TransitionsToSelectGameDirectory()
    {
        var next = AppStateMachine.OnSteamUpdate(AppState.SteamOffline, SteamStatus.Running, hasUser: true, hasGameDir: false);
        Assert.Equal(AppState.SelectGameDirectory, next);
    }

    // ── Sticky rule: VerifyingGame and Launcher survive Steam heartbeats ──────

    [Fact]
    public void SteamReady_WhileVerifyingGame_StaysVerifyingGame()
    {
        var next = AppStateMachine.OnSteamUpdate(AppState.VerifyingGame, SteamStatus.Running, hasUser: true, hasGameDir: true);
        Assert.Equal(AppState.VerifyingGame, next);
    }

    [Fact]
    public void SteamReady_WhileInLauncher_StaysLauncher()
    {
        var next = AppStateMachine.OnSteamUpdate(AppState.Launcher, SteamStatus.Running, hasUser: true, hasGameDir: true);
        Assert.Equal(AppState.Launcher, next);
    }

    [Fact]
    public void SteamReady_WhileInLauncher_NoGameDir_StaysLauncher()
    {
        // Even if hasGameDir is somehow false, sticky rule wins for Launcher
        var next = AppStateMachine.OnSteamUpdate(AppState.Launcher, SteamStatus.Running, hasUser: true, hasGameDir: false);
        Assert.Equal(AppState.Launcher, next);
    }

    [Fact]
    public void SteamStops_WhileVerifyingGame_TransitionsToSteamNotRunning()
    {
        var next = AppStateMachine.OnSteamUpdate(AppState.VerifyingGame, SteamStatus.NotRunning, hasUser: false, hasGameDir: true);
        Assert.Equal(AppState.SteamNotRunning, next);
    }

    [Fact]
    public void SteamStops_WhileInLauncher_TransitionsToSteamNotRunning()
    {
        var next = AppStateMachine.OnSteamUpdate(AppState.Launcher, SteamStatus.NotRunning, hasUser: false, hasGameDir: true);
        Assert.Equal(AppState.SteamNotRunning, next);
    }

    [Fact]
    public void SteamGoesOffline_WhileInLauncher_TransitionsToSteamOffline()
    {
        var next = AppStateMachine.OnSteamUpdate(AppState.Launcher, SteamStatus.Offline, hasUser: false, hasGameDir: true);
        Assert.Equal(AppState.SteamOffline, next);
    }

    // ── Action transitions ────────────────────────────────────────────────────

    [Theory]
    [InlineData(AppState.SelectGameDirectory)]
    [InlineData(AppState.CheckingSteam)]
    public void OnGameDirSelected_TransitionsToVerifyingGame(AppState current)
    {
        var next = AppStateMachine.OnGameDirSelected(current);
        Assert.Equal(AppState.VerifyingGame, next);
    }

    [Theory]
    [InlineData(AppState.VerifyingGame)]
    [InlineData(AppState.CheckingSteam)]
    public void OnVerificationCompleted_TransitionsToLauncher(AppState current)
    {
        var next = AppStateMachine.OnVerificationCompleted(current);
        Assert.Equal(AppState.Launcher, next);
    }

    [Theory]
    [InlineData(AppState.Launcher)]
    [InlineData(AppState.VerifyingGame)]
    public void OnGameDirChanged_TransitionsToVerifyingGame(AppState current)
    {
        var next = AppStateMachine.OnGameDirChanged(current);
        Assert.Equal(AppState.VerifyingGame, next);
    }

    // ── Typical startup sequences ─────────────────────────────────────────────

    [Fact]
    public void TypicalStartup_SteamAlreadyRunning_WithGameDir_ReachesVerifyingGame()
    {
        // App starts, Steam is already up, game dir known
        var s0 = AppStateMachine.OnSteamUpdate(AppState.CheckingSteam, SteamStatus.Running, hasUser: true, hasGameDir: true);
        Assert.Equal(AppState.VerifyingGame, s0);

        // Heartbeat arrives while verifying — must not restart
        var s1 = AppStateMachine.OnSteamUpdate(s0, SteamStatus.Running, hasUser: true, hasGameDir: true);
        Assert.Equal(AppState.VerifyingGame, s1);

        // Verification done → launcher
        var s2 = AppStateMachine.OnVerificationCompleted(s1);
        Assert.Equal(AppState.Launcher, s2);
    }

    [Fact]
    public void TypicalStartup_SteamNotRunning_ThenStarted_NoGameDir_ReachesSelectGame()
    {
        var s0 = AppStateMachine.OnSteamUpdate(AppState.CheckingSteam, SteamStatus.NotRunning, hasUser: false, hasGameDir: false);
        Assert.Equal(AppState.SteamNotRunning, s0);

        // User starts Steam, bridge finishes
        var s1 = AppStateMachine.OnSteamUpdate(s0, SteamStatus.Running, hasUser: true, hasGameDir: false);
        Assert.Equal(AppState.SelectGameDirectory, s1);

        // User picks a directory
        var s2 = AppStateMachine.OnGameDirSelected(s1);
        Assert.Equal(AppState.VerifyingGame, s2);

        // Verification done → launcher
        var s3 = AppStateMachine.OnVerificationCompleted(s2);
        Assert.Equal(AppState.Launcher, s3);
    }

    [Fact]
    public void GameDirChange_FromLauncher_TriggersReverification()
    {
        var s0 = AppState.Launcher;
        var s1 = AppStateMachine.OnGameDirChanged(s0);
        Assert.Equal(AppState.VerifyingGame, s1);

        var s2 = AppStateMachine.OnVerificationCompleted(s1);
        Assert.Equal(AppState.Launcher, s2);
    }
}
