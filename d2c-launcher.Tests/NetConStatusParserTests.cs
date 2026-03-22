using d2c_launcher.Integration;
using Xunit;

namespace d2c_launcher.Tests;

public class NetConStatusParserTests
{
    // ── ParseServerPort ───────────────────────────────────────────────────────

    [Fact]
    public void ParseServerPort_TypicalLine_ReturnsPort()
    {
        var line = "udp/ip  :  0.0.0.0:34720 os(Linux) type(dedicated)";
        Assert.Equal("34720", NetConStatusParser.ParseServerPort(line));
    }

    [Fact]
    public void ParseServerPort_HighPort_ReturnsPort()
    {
        var line = "udp/ip  :  0.0.0.0:65000 os(Linux) type(dedicated)";
        Assert.Equal("65000", NetConStatusParser.ParseServerPort(line));
    }

    [Fact]
    public void ParseServerPort_AlternativeOsToken_ReturnsPort()
    {
        // Regex matches any os(...) token — not Linux-specific
        var line = "udp/ip  :  0.0.0.0:27015 os(Windows) type(dedicated)";
        Assert.Equal("27015", NetConStatusParser.ParseServerPort(line));
    }

    [Fact]
    public void ParseServerPort_NoDedicatedMarker_StillReturnsPort()
    {
        // ParseServerPort only checks the regex — type(dedicated) filtering is
        // the caller's responsibility (the event handler in IsAlreadyConnectedToAsync).
        var line = "udp/ip  :  0.0.0.0:27015 os(Linux)";
        Assert.Equal("27015", NetConStatusParser.ParseServerPort(line));
    }

    [Fact]
    public void ParseServerPort_EmptyLine_ReturnsNull()
    {
        Assert.Null(NetConStatusParser.ParseServerPort(""));
    }

    [Fact]
    public void ParseServerPort_UnrelatedLine_ReturnsNull()
    {
        Assert.Null(NetConStatusParser.ParseServerPort("hostname: Dota 2"));
    }

    [Fact]
    public void ParseServerPort_PlayerListLine_ReturnsNull()
    {
        var line = "#      1 \"Player\" STEAM_0:1:12345678   00:45      45    0 active 10.8.1.1:27005";
        Assert.Null(NetConStatusParser.ParseServerPort(line));
    }

    // ── ExtractTargetPort ─────────────────────────────────────────────────────

    [Fact]
    public void ExtractTargetPort_Normal_ReturnsPort()
    {
        Assert.Equal("34720", NetConStatusParser.ExtractTargetPort("192.168.50.12:34720"));
    }

    [Fact]
    public void ExtractTargetPort_TrailingWhitespace_ReturnsTrimmed()
    {
        Assert.Equal("34720", NetConStatusParser.ExtractTargetPort("192.168.50.12:34720 "));
    }

    [Fact]
    public void ExtractTargetPort_NoColon_ReturnsNull()
    {
        Assert.Null(NetConStatusParser.ExtractTargetPort("192.168.50.12"));
    }

    [Fact]
    public void ExtractTargetPort_UsesLastColon()
    {
        // IPv6-style or double-colon — last colon wins
        Assert.Equal("27015", NetConStatusParser.ExtractTargetPort("::1:27015"));
    }

    [Fact]
    public void ExtractTargetPort_TrailingColonOnly_ReturnsEmpty()
    {
        // Malformed URL — returns "" (not null); caller comparison with ParseServerPort will be false
        Assert.Equal("", NetConStatusParser.ExtractTargetPort("192.168.50.12:"));
    }

    // ── port matching (integration of both) ──────────────────────────────────

    [Theory]
    [InlineData("udp/ip  :  0.0.0.0:34720 os(Linux) type(dedicated)", "192.168.50.12:34720", true)]
    [InlineData("udp/ip  :  0.0.0.0:34720 os(Linux) type(dedicated)", "192.168.50.12:27015", false)]
    [InlineData("udp/ip  :  0.0.0.0:27015 os(Windows) type(dedicated)", "10.0.0.1:27015", true)]
    [InlineData("hostname: Dota 2", "10.0.0.1:27015", false)]
    public void PortMatch_Scenarios(string statusLine, string serverUrl, bool expectedMatch)
    {
        var serverPort = NetConStatusParser.ParseServerPort(statusLine);
        var targetPort = NetConStatusParser.ExtractTargetPort(serverUrl);
        Assert.Equal(expectedMatch, serverPort != null && serverPort == targetPort);
    }
}
