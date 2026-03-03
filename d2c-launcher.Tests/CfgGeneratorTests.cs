using System.IO;
using d2c_launcher.Models;
using d2c_launcher.Services;
using Xunit;

namespace d2c_launcher.Tests;

public class CfgGeneratorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public CfgGeneratorTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Generate ──────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_NullCustomCfgLines_ReturnsNull()
    {
        var settings = new GameLaunchSettings { CustomCfgLines = null };
        var result = CfgGenerator.Generate(settings, _tempDir);
        Assert.Null(result);
    }

    [Fact]
    public void Generate_EmptyCustomCfgLines_ReturnsNull()
    {
        var settings = new GameLaunchSettings { CustomCfgLines = "" };
        var result = CfgGenerator.Generate(settings, _tempDir);
        Assert.Null(result);
    }

    [Fact]
    public void Generate_WhitespaceCustomCfgLines_ReturnsNull()
    {
        var settings = new GameLaunchSettings { CustomCfgLines = "   \n  " };
        var result = CfgGenerator.Generate(settings, _tempDir);
        Assert.Null(result);
    }

    [Fact]
    public void Generate_WithContent_ReturnsExecArgument()
    {
        var settings = new GameLaunchSettings { CustomCfgLines = "bind \"q\" \"nop\"" };
        var result = CfgGenerator.Generate(settings, _tempDir);
        Assert.Equal("+exec d2c_launch.cfg", result);
    }

    [Fact]
    public void Generate_WithContent_WritesCfgFileToDotaCfgDirectory()
    {
        var settings = new GameLaunchSettings { CustomCfgLines = "bind \"q\" \"nop\"" };
        CfgGenerator.Generate(settings, _tempDir);

        var expectedPath = Path.Combine(_tempDir, "dota", "cfg", "d2c_launch.cfg");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public void Generate_WithContent_FileContainsTrimmedContentPlusNewline()
    {
        const string lines = "  bind \"q\" \"nop\"  ";
        var settings = new GameLaunchSettings { CustomCfgLines = lines };
        CfgGenerator.Generate(settings, _tempDir);

        var expectedPath = Path.Combine(_tempDir, "dota", "cfg", "d2c_launch.cfg");
        var written = File.ReadAllText(expectedPath);
        Assert.Equal("bind \"q\" \"nop\"\n", written);
    }

    [Fact]
    public void Generate_CreatesIntermediateDirectories()
    {
        // gameDirectory with a non-existing nested path
        var nested = Path.Combine(_tempDir, "game");
        var settings = new GameLaunchSettings { CustomCfgLines = "con_enable 1" };
        var result = CfgGenerator.Generate(settings, nested);

        Assert.Equal("+exec d2c_launch.cfg", result);
        Assert.True(Directory.Exists(Path.Combine(nested, "dota", "cfg")));
    }

    // ── BuildCliArgs ──────────────────────────────────────────────────────────

    [Fact]
    public void BuildCliArgs_Defaults_ReturnsLanguageOnly()
    {
        var settings = new GameLaunchSettings();
        // Defaults: NoVid=false, Language="russian", ExtraArgs=null
        var result = CfgGenerator.BuildCliArgs(settings);
        Assert.Equal("-language russian", result);
    }

    [Fact]
    public void BuildCliArgs_NoVid_IncludesFlag()
    {
        var settings = new GameLaunchSettings { NoVid = true };
        var result = CfgGenerator.BuildCliArgs(settings);
        Assert.Contains("-novid", result);
    }

    [Fact]
    public void BuildCliArgs_NoVidFalse_DoesNotIncludeFlag()
    {
        var settings = new GameLaunchSettings { NoVid = false, Language = "" };
        var result = CfgGenerator.BuildCliArgs(settings);
        Assert.DoesNotContain("-novid", result);
    }

    [Fact]
    public void BuildCliArgs_Language_IncludesLanguageFlag()
    {
        var settings = new GameLaunchSettings { Language = "english" };
        var result = CfgGenerator.BuildCliArgs(settings);
        Assert.Contains("-language english", result);
    }

    [Fact]
    public void BuildCliArgs_EmptyLanguage_OmitsLanguageFlag()
    {
        var settings = new GameLaunchSettings { Language = "" };
        var result = CfgGenerator.BuildCliArgs(settings);
        Assert.DoesNotContain("-language", result);
    }

    [Fact]
    public void BuildCliArgs_ExtraArgs_Appended()
    {
        var settings = new GameLaunchSettings { Language = "", ExtraArgs = "-dx11" };
        var result = CfgGenerator.BuildCliArgs(settings);
        Assert.Contains("-dx11", result);
    }

    [Fact]
    public void BuildCliArgs_ExtraArgs_Trimmed()
    {
        var settings = new GameLaunchSettings { Language = "", ExtraArgs = "  -dx11  " };
        var result = CfgGenerator.BuildCliArgs(settings);
        Assert.Equal("-dx11", result);
    }

    [Fact]
    public void BuildCliArgs_AllOptions_CorrectOrder()
    {
        var settings = new GameLaunchSettings
        {
            NoVid = true,
            Language = "english",
            ExtraArgs = "-dx11",
        };
        var result = CfgGenerator.BuildCliArgs(settings);
        Assert.Equal("-novid -language english -dx11", result);
    }

    [Fact]
    public void BuildCliArgs_NoOptions_ReturnsEmpty()
    {
        var settings = new GameLaunchSettings { NoVid = false, Language = "", ExtraArgs = null };
        var result = CfgGenerator.BuildCliArgs(settings);
        Assert.Equal("", result);
    }
}
