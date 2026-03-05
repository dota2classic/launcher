using System.IO;
using d2c_launcher.Models;
using d2c_launcher.Services;
using Xunit;

namespace d2c_launcher.Tests;

/// <summary>
/// Tests for DotaVideoTxtReader (parsing), DotaVideoTxtWriter (file update),
/// and their roundtrip behaviour.
/// Uses temporary directories mirroring the real layout: {gameDir}/dota/cfg/video.txt
/// </summary>
public class DotaVideoTxtReaderTests
{
    // ── ParseKvLine ───────────────────────────────────────────────────────────

    [Fact]
    public void ParseKvLine_ValidLine_ReturnsKeyValue()
    {
        var result = DotaVideoTxtReader.ParseKvLine("\t\"setting.fullscreen\"\t\t\"1\"");
        Assert.NotNull(result);
        Assert.Equal("setting.fullscreen", result!.Value.Key);
        Assert.Equal("1", result.Value.Value);
    }

    [Fact]
    public void ParseKvLine_LeadingWhitespace_Trimmed()
    {
        var result = DotaVideoTxtReader.ParseKvLine("   \"key\"\t\"val\"");
        Assert.NotNull(result);
        Assert.Equal("key", result!.Value.Key);
        Assert.Equal("val", result.Value.Value);
    }

    [Fact]
    public void ParseKvLine_NoQuotes_ReturnsNull()
    {
        var result = DotaVideoTxtReader.ParseKvLine("setting.fullscreen 1");
        Assert.Null(result);
    }

    [Fact]
    public void ParseKvLine_BraceOnly_ReturnsNull()
    {
        Assert.Null(DotaVideoTxtReader.ParseKvLine("{"));
        Assert.Null(DotaVideoTxtReader.ParseKvLine("}"));
    }

    [Fact]
    public void ParseKvLine_EmptyLine_ReturnsNull()
    {
        Assert.Null(DotaVideoTxtReader.ParseKvLine(""));
        Assert.Null(DotaVideoTxtReader.ParseKvLine("   "));
    }

    [Fact]
    public void ParseKvLine_SectionHeader_ReturnsNull()
    {
        // e.g. "config" — only one quoted token, no value
        Assert.Null(DotaVideoTxtReader.ParseKvLine("\"config\""));
    }

    [Fact]
    public void ParseKvLine_EmptyValue_ReturnsKeyWithEmptyString()
    {
        var result = DotaVideoTxtReader.ParseKvLine("\"key\"\t\"\"");
        Assert.NotNull(result);
        Assert.Equal("key", result!.Value.Key);
        Assert.Equal("", result.Value.Value);
    }

    // ── ParseKvPairs ──────────────────────────────────────────────────────────

    [Fact]
    public void ParseKvPairs_RealVideoTxt_ExtractsAllSettings()
    {
        const string content = """
            "config"
            {
            	"setting.fullscreen"		"1"
            	"setting.nowindowborder"	"0"
            	"setting.defaultres"		"1920"
            	"setting.defaultresheight"	"1080"
            	"setting.aspectratiomode"	"1"
            }
            """;

        var kv = DotaVideoTxtReader.ParseKvPairs(content);

        Assert.Equal("1", kv["setting.fullscreen"]);
        Assert.Equal("0", kv["setting.nowindowborder"]);
        Assert.Equal("1920", kv["setting.defaultres"]);
        Assert.Equal("1080", kv["setting.defaultresheight"]);
        Assert.Equal("1", kv["setting.aspectratiomode"]);
    }

    [Fact]
    public void ParseKvPairs_IsCaseInsensitive()
    {
        var kv = DotaVideoTxtReader.ParseKvPairs("\"Setting.Fullscreen\"\t\"1\"");
        Assert.True(kv.ContainsKey("setting.fullscreen"));
    }

    [Fact]
    public void ParseKvPairs_EmptyContent_ReturnsEmpty()
    {
        var kv = DotaVideoTxtReader.ParseKvPairs("");
        Assert.Empty(kv);
    }
}

public class DotaVideoTxtWriterTests : IDisposable
{
    private readonly string _gameDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string _cfgDir;
    private readonly string _videoPath;

    public DotaVideoTxtWriterTests()
    {
        _cfgDir = Path.Combine(_gameDir, "dota", "cfg");
        _videoPath = Path.Combine(_cfgDir, "video.txt");
        Directory.CreateDirectory(_cfgDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_gameDir))
            Directory.Delete(_gameDir, recursive: true);
    }

    private void WriteVideo(string content) => File.WriteAllText(_videoPath, content);
    private string ReadVideo() => File.ReadAllText(_videoPath);

    // ── AspectRatioMode ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(1920, 1080, 1)] // 16:9
    [InlineData(1280, 720,  1)] // 16:9
    [InlineData(2560, 1440, 1)] // 16:9
    [InlineData(1680, 1050, 2)] // 16:10
    [InlineData(1280, 800,  2)] // 16:10
    [InlineData(1024, 768,  0)] // 4:3
    [InlineData(800,  600,  0)] // 4:3
    [InlineData(1280, 1024, 0)] // 5:4
    [InlineData(1920, 0,    1)] // height zero → default 16:9
    public void AspectRatioMode_ReturnsCorrectMode(int width, int height, int expected)
    {
        Assert.Equal(expected, DotaVideoTxtWriter.AspectRatioMode(width, height));
    }

    // ── No existing file — creates minimal file ───────────────────────────────

    [Fact]
    public void NoExistingFile_CreatesFileWithSettings()
    {
        var settings = new VideoSettings { Fullscreen = true, Width = 1920, Height = 1080 };
        DotaVideoTxtWriter.Write(_gameDir, settings);

        Assert.True(File.Exists(_videoPath));
        var content = ReadVideo();
        Assert.Contains("setting.fullscreen", content);
        Assert.Contains("setting.defaultres", content);
        Assert.Contains("setting.defaultresheight", content);
    }

    [Fact]
    public void NoExistingFile_CreatesIntermediateDirectories()
    {
        var nested = Path.Combine(_gameDir, "sub");
        DotaVideoTxtWriter.Write(nested, new VideoSettings { Width = 1920, Height = 1080 });
        Assert.True(File.Exists(Path.Combine(nested, "dota", "cfg", "video.txt")));
    }

    // ── Update existing file — values updated, other keys preserved ───────────

    [Fact]
    public void ExistingFile_FullscreenUpdated()
    {
        WriteVideo("\"config\"\n{\n\t\"setting.fullscreen\"\t\t\"0\"\n}\n");

        DotaVideoTxtWriter.Write(_gameDir, new VideoSettings { Fullscreen = true, Width = 1920, Height = 1080 });

        Assert.Contains("\"setting.fullscreen\"\t\t\"1\"", ReadVideo());
    }

    [Fact]
    public void ExistingFile_ResolutionUpdated()
    {
        WriteVideo("\"config\"\n{\n\t\"setting.defaultres\"\t\t\"1280\"\n\t\"setting.defaultresheight\"\t\t\"720\"\n}\n");

        DotaVideoTxtWriter.Write(_gameDir, new VideoSettings { Width = 1920, Height = 1080 });

        var content = ReadVideo();
        Assert.Contains("\"1920\"", content);
        Assert.Contains("\"1080\"", content);
        Assert.DoesNotContain("\"1280\"", content);
        Assert.DoesNotContain("\"720\"", content);
    }

    [Fact]
    public void ExistingFile_UnrelatedKeysPreserved()
    {
        WriteVideo("\"config\"\n{\n\t\"setting.fullscreen\"\t\t\"0\"\n\t\"setting.mat_antialias\"\t\t\"2\"\n}\n");

        DotaVideoTxtWriter.Write(_gameDir, new VideoSettings { Width = 1920, Height = 1080 });

        Assert.Contains("setting.mat_antialias", ReadVideo());
    }

    [Fact]
    public void ExistingFile_MissingKey_InsertedBeforeClosingBrace()
    {
        // File has no setting.nowindowborder — it should be inserted
        WriteVideo("\"config\"\n{\n\t\"setting.fullscreen\"\t\t\"0\"\n}\n");

        DotaVideoTxtWriter.Write(_gameDir, new VideoSettings { NoWindowBorder = true, Width = 1920, Height = 1080 });

        var content = ReadVideo();
        Assert.Contains("setting.nowindowborder", content);
        // Closing brace must still be present
        Assert.Contains("}", content);
    }

    // ── Roundtrip ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true,  false, 1920, 1080)]
    [InlineData(false, true,  1280, 800)]
    [InlineData(false, false, 800,  600)]
    public void WriteRead_Roundtrip(bool fullscreen, bool noWindowBorder, int width, int height)
    {
        var original = new VideoSettings
        {
            Fullscreen = fullscreen,
            NoWindowBorder = noWindowBorder,
            Width = width,
            Height = height,
        };

        DotaVideoTxtWriter.Write(_gameDir, original);
        var restored = DotaVideoTxtReader.Read(_gameDir);

        Assert.NotNull(restored);
        Assert.Equal(fullscreen, restored!.Fullscreen);
        Assert.Equal(noWindowBorder, restored.NoWindowBorder);
        Assert.Equal(width, restored.Width);
        Assert.Equal(height, restored.Height);
    }

    [Fact]
    public void WriteRead_UpdateExisting_Roundtrip()
    {
        // Write once, then overwrite with different values, then read back
        DotaVideoTxtWriter.Write(_gameDir, new VideoSettings { Fullscreen = false, Width = 1280, Height = 720 });
        DotaVideoTxtWriter.Write(_gameDir, new VideoSettings { Fullscreen = true, Width = 1920, Height = 1080 });

        var result = DotaVideoTxtReader.Read(_gameDir);

        Assert.NotNull(result);
        Assert.True(result!.Fullscreen);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
    }
}

public class DotaVideoTxtReaderFileTests : IDisposable
{
    private readonly string _gameDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string _cfgDir;

    public DotaVideoTxtReaderFileTests()
    {
        _cfgDir = Path.Combine(_gameDir, "dota", "cfg");
        Directory.CreateDirectory(_cfgDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_gameDir))
            Directory.Delete(_gameDir, recursive: true);
    }

    private void WriteVideo(string content)
        => File.WriteAllText(Path.Combine(_cfgDir, "video.txt"), content);

    [Fact]
    public void Read_MissingFile_ReturnsNull()
    {
        var result = DotaVideoTxtReader.Read(_gameDir);
        Assert.Null(result);
    }

    [Fact]
    public void Read_EmptyFile_ReturnsDefaultSettings()
    {
        WriteVideo("");
        var result = DotaVideoTxtReader.Read(_gameDir);
        Assert.NotNull(result);
    }

    [Fact]
    public void Read_FullscreenTrue_Parsed()
    {
        WriteVideo("\"config\"\n{\n\t\"setting.fullscreen\"\t\t\"1\"\n}\n");
        var result = DotaVideoTxtReader.Read(_gameDir);
        Assert.True(result!.Fullscreen);
    }

    [Fact]
    public void Read_FullscreenFalse_Parsed()
    {
        WriteVideo("\"config\"\n{\n\t\"setting.fullscreen\"\t\t\"0\"\n}\n");
        var result = DotaVideoTxtReader.Read(_gameDir);
        Assert.False(result!.Fullscreen);
    }

    [Fact]
    public void Read_Resolution_Parsed()
    {
        WriteVideo("\"config\"\n{\n\t\"setting.defaultres\"\t\t\"1920\"\n\t\"setting.defaultresheight\"\t\t\"1080\"\n}\n");
        var result = DotaVideoTxtReader.Read(_gameDir);
        Assert.Equal(1920, result!.Width);
        Assert.Equal(1080, result.Height);
    }

    [Fact]
    public void Read_InvalidResolution_IgnoredKeepsDefault()
    {
        WriteVideo("\"config\"\n{\n\t\"setting.defaultres\"\t\t\"notanumber\"\n}\n");
        var result = DotaVideoTxtReader.Read(_gameDir);
        // Width should remain the VideoSettings default, not throw
        Assert.NotNull(result);
    }

    [Fact]
    public void Read_ZeroResolution_IgnoredKeepsDefault()
    {
        var defaults = new VideoSettings();
        WriteVideo("\"config\"\n{\n\t\"setting.defaultres\"\t\t\"0\"\n\t\"setting.defaultresheight\"\t\t\"0\"\n}\n");
        var result = DotaVideoTxtReader.Read(_gameDir);
        // Zero is not a valid resolution; reader should ignore it
        Assert.Equal(defaults.Width, result!.Width);
        Assert.Equal(defaults.Height, result.Height);
    }
}
