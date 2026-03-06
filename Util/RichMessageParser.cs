using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using d2c_launcher.Models;

namespace d2c_launcher.Util;

/// <summary>
/// Parses a raw chat message string into a list of <see cref="RichSegment"/> items
/// for rendering as mixed text/emoticon/rarity/URL content.
/// Mirrors the rule-based approach of the web client's RichMessage component.
/// </summary>
public static class RichMessageParser
{
    // Pre-process: collapse multiple blank lines and strip markdown link syntax.
    private static readonly Regex s_multiBlankLine = new(@"\n\s*\n", RegexOptions.Compiled);
    private static readonly Regex s_markdownLink   = new(@"\[([^\[\]]*)\]\((.*?)\)", RegexOptions.Compiled);

    // Rule regexes (applied in order).
    private static readonly Regex s_rarity = new(
        @"<(common|uncommon|rare|mythical|immortal|legendary|arcana|ancient)>(.*?)<\/\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex s_emoticon = new(@":([a-zA-Z0-9_+\-]+):", RegexOptions.Compiled);
    private static readonly Regex s_playerLink = new(
        @"https://dotaclassic\.ru/players/(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex s_url      = new(@"https?://[^\s]+", RegexOptions.Compiled);

    public static IReadOnlyList<RichSegment> Parse(
        string rawMessage,
        IReadOnlyDictionary<string, Bitmap>? emoticons = null,
        IReadOnlyDictionary<string, string?>? userNames = null)
    {
        // Pre-process
        var msg = s_markdownLink.Replace(rawMessage, m => m.Groups[2].Value);
        msg = s_multiBlankLine.Replace(msg, "\n");

        // Start with the entire message as a single string token.
        var tokens = new List<object> { (object)msg };

        // Apply rarity rule
        ApplyRule(tokens, s_rarity, m =>
        {
            var tag = m.Groups[1].Value;
            var content = m.Groups[2].Value;
            return new RaritySegment(content, tag);
        });

        // Apply emoticon rule
        ApplyRule(tokens, s_emoticon, m =>
        {
            var code = m.Groups[1].Value;
            Bitmap? bitmap = null;
            emoticons?.TryGetValue(code, out bitmap);
            return new EmoticonSegment(code, bitmap);
        });

        // Apply player link rule (before generic URL rule)
        ApplyRule(tokens, s_playerLink, m =>
        {
            var steamId = m.Groups[1].Value;
            var displayName = (userNames != null && userNames.TryGetValue(steamId, out var n) && n != null)
                ? $"@{n}"
                : "Загрузка...";
            return new PlayerLinkSegment(steamId, m.Value, displayName);
        });

        // Apply URL rule
        ApplyRule(tokens, s_url, m => new UrlSegment(m.Value));

        // Convert remaining string tokens to TextSegments
        var result = new List<RichSegment>(tokens.Count);
        foreach (var token in tokens)
        {
            result.Add(token is RichSegment seg
                ? seg
                : new TextSegment((string)token));
        }
        return result;
    }

    private static void ApplyRule(
        List<object> tokens,
        Regex regex,
        Func<Match, RichSegment> render)
    {
        var newTokens = new List<object>(tokens.Count);

        foreach (var token in tokens)
        {
            if (token is not string str)
            {
                newTokens.Add(token);
                continue;
            }

            var lastIndex = 0;
            foreach (Match m in regex.Matches(str))
            {
                if (m.Index > lastIndex)
                    newTokens.Add(str[lastIndex..m.Index]);
                newTokens.Add(render(m));
                lastIndex = m.Index + m.Length;
            }
            if (lastIndex < str.Length)
                newTokens.Add(str[lastIndex..]);
        }

        tokens.Clear();
        tokens.AddRange(newTokens);
    }
}
