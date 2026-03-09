using System.Collections.Generic;
using System.IO;
using d2c_launcher.Services;
using Xunit;

namespace d2c_launcher.Tests;

/// <summary>
/// Tests for DotaCfgWriter using temporary directories that mirror the
/// real config layout: {gameDirectory}/dota/cfg/config.cfg
/// </summary>
public class DotaCfgWriterTests : IDisposable
{
    private readonly string _gameDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string _cfgDir;
    private readonly string _cfgPath;

    public DotaCfgWriterTests()
    {
        _cfgDir = Path.Combine(_gameDir, "dota", "cfg");
        _cfgPath = Path.Combine(_cfgDir, "config.cfg");
        Directory.CreateDirectory(_cfgDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_gameDir))
            Directory.Delete(_gameDir, recursive: true);
    }

    private void WriteCfg(string content)
        => File.WriteAllText(_cfgPath, content);

    private string ReadCfg()
        => File.ReadAllText(_cfgPath);

    // ── No existing file ──────────────────────────────────────────────────────

    [Fact]
    public void NoExistingFile_CreatesCfgWithCvars()
    {
        File.Delete(_cfgPath);

        DotaCfgWriter.WriteCvars(_gameDir, new Dictionary<string, string>
        {
            ["con_enable"] = "1",
        });

        Assert.True(File.Exists(_cfgPath));
        Assert.Contains("con_enable \"1\"", ReadCfg());
    }

    [Fact]
    public void NoExistingFile_CreatesIntermediateDirectories()
    {
        var nested = Path.Combine(_gameDir, "sub");
        DotaCfgWriter.WriteCvars(nested, new Dictionary<string, string>
        {
            ["con_enable"] = "1",
        });

        Assert.True(File.Exists(Path.Combine(nested, "dota", "cfg", "config.cfg")));
    }

    [Fact]
    public void NoExistingFile_MultipleCtars_AllWritten()
    {
        File.Delete(_cfgPath);

        DotaCfgWriter.WriteCvars(_gameDir, new Dictionary<string, string>
        {
            ["con_enable"] = "1",
            ["fps_max"] = "144",
        });

        var content = ReadCfg();
        Assert.Contains("con_enable \"1\"", content);
        Assert.Contains("fps_max \"144\"", content);
    }

    // ── Update existing cvars in-place ────────────────────────────────────────

    [Fact]
    public void ExistingCvar_UpdatesValueInPlace()
    {
        WriteCfg("con_enable \"0\"\n");

        DotaCfgWriter.WriteCvars(_gameDir, new Dictionary<string, string>
        {
            ["con_enable"] = "1",
        });

        var content = ReadCfg();
        Assert.Contains("con_enable \"1\"", content);
        // Should not duplicate the line
        Assert.Equal(1, CountOccurrences(content, "con_enable"));
    }

    [Fact]
    public void ExistingCvar_QuotedValue_UpdatesValueInPlace()
    {
        // config.cfg may or may not quote the existing value — both should be updated
        WriteCfg("fps_max \"120\"\n");

        DotaCfgWriter.WriteCvars(_gameDir, new Dictionary<string, string>
        {
            ["fps_max"] = "240",
        });

        var content = ReadCfg();
        Assert.Contains("fps_max \"240\"", content);
        Assert.DoesNotContain("fps_max \"120\"", content);
    }

    [Fact]
    public void ExistingCvar_CaseInsensitive_UpdatesInPlace()
    {
        WriteCfg("CON_ENABLE \"0\"\n");

        DotaCfgWriter.WriteCvars(_gameDir, new Dictionary<string, string>
        {
            ["con_enable"] = "1",
        });

        Assert.Equal(1, CountOccurrences(ReadCfg(), "\"1\""));
        Assert.Equal(0, CountOccurrences(ReadCfg(), "\"0\""));
    }

    // ── Append missing cvars ──────────────────────────────────────────────────

    [Fact]
    public void MissingCvar_AppendedAtEnd()
    {
        WriteCfg("con_enable \"1\"\n");

        DotaCfgWriter.WriteCvars(_gameDir, new Dictionary<string, string>
        {
            ["fps_max"] = "144",
        });

        var content = ReadCfg();
        Assert.Contains("fps_max \"144\"", content);
    }

    [Fact]
    public void MixedExistingAndMissing_UpdatesExistingAndAppendsMissing()
    {
        WriteCfg("con_enable \"0\"\n");

        DotaCfgWriter.WriteCvars(_gameDir, new Dictionary<string, string>
        {
            ["con_enable"] = "1",
            ["fps_max"] = "144",
        });

        var content = ReadCfg();
        Assert.Contains("con_enable \"1\"", content);
        Assert.Contains("fps_max \"144\"", content);
        Assert.Equal(1, CountOccurrences(content, "con_enable"));
    }

    // ── Non-managed lines preserved ───────────────────────────────────────────

    [Fact]
    public void CommentLines_Preserved()
    {
        WriteCfg("// this is a comment\ncon_enable \"0\"\n");

        DotaCfgWriter.WriteCvars(_gameDir, new Dictionary<string, string>
        {
            ["con_enable"] = "1",
        });

        Assert.Contains("// this is a comment", ReadCfg());
    }

    [Fact]
    public void BindLines_Preserved()
    {
        WriteCfg("bind \"q\" \"dota_ability_execute 0\"\ncon_enable \"0\"\n");

        DotaCfgWriter.WriteCvars(_gameDir, new Dictionary<string, string>
        {
            ["con_enable"] = "1",
        });

        Assert.Contains("bind \"q\" \"dota_ability_execute 0\"", ReadCfg());
    }

    [Fact]
    public void UnknownCvars_Preserved()
    {
        WriteCfg("sensitivity \"3\"\ncon_enable \"0\"\n");

        DotaCfgWriter.WriteCvars(_gameDir, new Dictionary<string, string>
        {
            ["con_enable"] = "1",
        });

        Assert.Contains("sensitivity \"3\"", ReadCfg());
    }

    [Fact]
    public void BlankLines_Preserved()
    {
        WriteCfg("\ncon_enable \"0\"\n\n");

        DotaCfgWriter.WriteCvars(_gameDir, new Dictionary<string, string>
        {
            ["con_enable"] = "1",
        });

        // The file should still contain empty lines (at least one blank line preserved)
        var lines = ReadCfg().Split('\n');
        Assert.Contains(lines, l => l.Trim() == "");
    }

    // ── Real-world config excerpt ─────────────────────────────────────────────

    [Fact]
    public void RealConfigExcerpt_UpdatesKnownCvarsAndPreservesRest()
    {
        const string before = """
            unbindall
            bind "q" "dota_ability_execute 5"
            con_enable "0"
            fps_max "120"
            sensitivity "3"
            """;
        WriteCfg(before);

        DotaCfgWriter.WriteCvars(_gameDir, new Dictionary<string, string>
        {
            ["con_enable"] = "1",
            ["fps_max"] = "240",
        });

        var content = ReadCfg();
        Assert.Contains("con_enable \"1\"", content);
        Assert.Contains("fps_max \"240\"", content);
        Assert.Contains("unbindall", content);
        Assert.Contains("bind \"q\" \"dota_ability_execute 5\"", content);
        Assert.Contains("sensitivity \"3\"", content);
        Assert.DoesNotContain("con_enable \"0\"", content);
        Assert.DoesNotContain("fps_max \"120\"", content);
    }

    // ── Trailing empty lines (regression) ────────────────────────────────────

    [Fact]
    public void RepeatedWrites_DoNotAccumulateTrailingEmptyLines()
    {
        WriteCfg("con_enable \"0\"\n");

        var cvars = new Dictionary<string, string> { ["con_enable"] = "1" };

        DotaCfgWriter.WriteCvars(_gameDir, cvars);
        DotaCfgWriter.WriteCvars(_gameDir, cvars);
        DotaCfgWriter.WriteCvars(_gameDir, cvars);

        var content = ReadCfg();
        // After the last non-empty line there should be exactly one newline
        Assert.Equal(content.TrimEnd('\n', '\r'), content.TrimEnd());
        var trailingNewlines = content.Length - content.TrimEnd('\n').Length;
        Assert.Equal(1, trailingNewlines);
    }

    [Fact]
    public void CrlfLineEndings_DoNotAccumulateTrailingEmptyLines()
    {
        // Dota writes config.cfg with CRLF on Windows
        WriteCfg("con_enable \"0\"\r\nfps_max \"120\"\r\n");

        var cvars = new Dictionary<string, string>
        {
            ["con_enable"] = "1",
            ["fps_max"] = "144",
        };

        DotaCfgWriter.WriteCvars(_gameDir, cvars);
        DotaCfgWriter.WriteCvars(_gameDir, cvars);

        var content = ReadCfg();
        var trailingNewlines = content.Length - content.TrimEnd('\n').Length;
        Assert.Equal(1, trailingNewlines);
    }

    [Fact]
    public void SingleWrite_EndsWithExactlyOneNewline()
    {
        File.Delete(_cfgPath);

        DotaCfgWriter.WriteCvars(_gameDir, new Dictionary<string, string>
        {
            ["con_enable"] = "1",
        });

        var content = ReadCfg();
        Assert.True(content.EndsWith("\n"), "file should end with newline");
        Assert.False(content.EndsWith("\n\n"), "file should not end with double newline");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(pattern, idx, System.StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}
