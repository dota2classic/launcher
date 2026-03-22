using System.Text.RegularExpressions;

namespace d2c_launcher.Integration;

/// <summary>
/// Parses lines produced by the Source 1 engine <c>status</c> console command.
/// </summary>
public static class NetConStatusParser
{
    // Matches the server port from a "status" line like:
    //   udp/ip  :  0.0.0.0:34720 os(Linux) type(dedicated)
    // Captures the digits between the last colon and " os(".
    private static readonly Regex s_udpPortRegex =
        new(@":(\d+)\s+os\(", RegexOptions.Compiled);

    /// <summary>
    /// Extracts the server port string from a Source engine <c>status</c> line containing <c>os(</c>.
    /// Returns <c>null</c> if the line does not match the expected format.
    /// Note: this method does not check for <c>type(dedicated)</c> — that filtering is the caller's responsibility.
    /// </summary>
    public static string? ParseServerPort(string line)
    {
        var match = s_udpPortRegex.Match(line);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Extracts and trims the port from a server URL in <c>ip:port</c> format.
    /// Returns <c>null</c> if the URL contains no colon.
    /// </summary>
    public static string? ExtractTargetPort(string serverUrl)
    {
        var colonIdx = serverUrl.LastIndexOf(':');
        if (colonIdx < 0) return null;
        return serverUrl[(colonIdx + 1)..].Trim();
    }
}
