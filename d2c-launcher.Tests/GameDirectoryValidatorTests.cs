using System.IO;
using d2c_launcher.Services;
using Xunit;

namespace d2c_launcher.Tests;

/// <summary>
/// Tests for <see cref="GameDirectoryValidator.IsAcceptable"/>.
/// </summary>
public class GameDirectoryValidatorTests
{
    // ── Fresh / empty directories ─────────────────────────────────────────────

    [Fact]
    public void EmptyDirectory_IsAccepted()
    {
        var dir = CreateTempDir();
        Assert.True(GameDirectoryValidator.IsAcceptable(dir, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void NonExistentDirectory_IsAccepted()
    {
        // A path that doesn't exist yet is treated as a fresh install target.
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Assert.True(GameDirectoryValidator.IsAcceptable(dir, out var error));
        Assert.Null(error);
    }

    // ── Source 1 patch version checks ────────────────────────────────────────

    [Fact]
    public void CorrectPatchVersion_IsAccepted()
    {
        var dir = CreateTempDirWithSteamInf(patchVersion: 41);
        Assert.True(GameDirectoryValidator.IsAcceptable(dir, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void WrongPatchVersion_IsRejected()
    {
        var dir = CreateTempDirWithSteamInf(patchVersion: 99);
        Assert.False(GameDirectoryValidator.IsAcceptable(dir, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void GameinfoPresentButNoSteamInf_IsAccepted()
    {
        // gameinfo.txt without steam.inf → assume it's already our build.
        var dir = CreateTempDir();
        var gameInfoDir = Path.Combine(dir, "dota");
        Directory.CreateDirectory(gameInfoDir);
        File.WriteAllText(Path.Combine(gameInfoDir, "gameinfo.txt"), "");

        Assert.True(GameDirectoryValidator.IsAcceptable(dir, out var error));
        Assert.Null(error);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string CreateTempDirWithSteamInf(int patchVersion)
    {
        var dir = CreateTempDir();
        var dotaDir = Path.Combine(dir, "dota");
        Directory.CreateDirectory(dotaDir);
        File.WriteAllText(Path.Combine(dotaDir, "gameinfo.txt"), "");
        File.WriteAllText(Path.Combine(dotaDir, "steam.inf"), $"PatchVersion={patchVersion}\n");
        return dir;
    }
}
