using System.Collections.Generic;
using d2c_launcher.Models;
using d2c_launcher.Services;
using Xunit;

namespace d2c_launcher.Tests;

public class ManifestDiffServiceTests
{
    private readonly ManifestDiffService _sut = new();

    // ── Exact mode ────────────────────────────────────────────────────────────

    [Fact]
    public void ExactMode_FileAbsent_RequiresDownload()
    {
        var remote = Manifest(ExactFile("game/dota.exe", "aaa"));
        var local = Manifest();

        var result = _sut.ComputeFilesToDownload(remote, local);

        Assert.Single(result);
        Assert.Equal("game/dota.exe", result[0].Path);
    }

    [Fact]
    public void ExactMode_HashMatch_NoDownload()
    {
        var remote = Manifest(ExactFile("game/dota.exe", "aaa"));
        var local = Manifest(ExactFile("game/dota.exe", "aaa"));

        var result = _sut.ComputeFilesToDownload(remote, local);

        Assert.Empty(result);
    }

    [Fact]
    public void ExactMode_HashMismatch_RequiresDownload()
    {
        var remote = Manifest(ExactFile("game/dota.exe", "aaa"));
        var local = Manifest(ExactFile("game/dota.exe", "bbb"));

        var result = _sut.ComputeFilesToDownload(remote, local);

        Assert.Single(result);
    }

    // ── Existing mode ─────────────────────────────────────────────────────────

    [Fact]
    public void ExistingMode_FileAbsent_RequiresDownload()
    {
        var remote = Manifest(ExistingFile("cfg/autoexec.cfg", "aaa"));
        var local = Manifest();

        var result = _sut.ComputeFilesToDownload(remote, local);

        Assert.Single(result);
    }

    [Fact]
    public void ExistingMode_FilePresent_NoDownload()
    {
        var remote = Manifest(ExistingFile("cfg/autoexec.cfg", "aaa"));
        var local = Manifest(ExistingFile("cfg/autoexec.cfg", "aaa"));

        var result = _sut.ComputeFilesToDownload(remote, local);

        Assert.Empty(result);
    }

    [Fact]
    public void ExistingMode_HashMismatch_NoDownload()
    {
        // For "existing" files hash differences are irrelevant — file just needs to exist
        var remote = Manifest(ExistingFile("cfg/autoexec.cfg", "aaa"));
        var local = Manifest(ExistingFile("cfg/autoexec.cfg", "completely_different_hash"));

        var result = _sut.ComputeFilesToDownload(remote, local);

        Assert.Empty(result);
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyLocal_AllRemoteRequired()
    {
        var remote = Manifest(
            ExactFile("a.txt", "1"),
            ExactFile("b.txt", "2"),
            ExistingFile("c.cfg", "3"));
        var local = Manifest();

        var result = _sut.ComputeFilesToDownload(remote, local);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void EmptyRemote_NothingRequired()
    {
        var remote = Manifest();
        var local = Manifest(ExactFile("a.txt", "1"));

        var result = _sut.ComputeFilesToDownload(remote, local);

        Assert.Empty(result);
    }

    [Fact]
    public void PathComparison_CaseInsensitive()
    {
        // Windows FS is case-insensitive; local scan may yield different casing
        var remote = Manifest(ExactFile("Foo/Bar.txt", "abc"));
        var local = Manifest(ExactFile("foo/bar.txt", "abc"));

        var result = _sut.ComputeFilesToDownload(remote, local);

        Assert.Empty(result);
    }

    [Fact]
    public void HashComparison_CaseInsensitive()
    {
        var remote = Manifest(ExactFile("file.bin", "ABCDEF123"));
        var local = Manifest(ExactFile("file.bin", "abcdef123"));

        var result = _sut.ComputeFilesToDownload(remote, local);

        Assert.Empty(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GameManifest Manifest(params GameManifestFile[] files) =>
        new() { Files = new List<GameManifestFile>(files) };

    private static GameManifestFile ExactFile(string path, string hash) =>
        new() { Path = path, Hash = hash, Size = 100, Mode = "exact" };

    private static GameManifestFile ExistingFile(string path, string hash) =>
        new() { Path = path, Hash = hash, Size = 100, Mode = "existing" };
}
