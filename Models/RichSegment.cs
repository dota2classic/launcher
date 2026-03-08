namespace d2c_launcher.Models;

public abstract class RichSegment { }

public sealed class TextSegment : RichSegment
{
    public string Text { get; }
    public TextSegment(string text) => Text = text;
}

public sealed class RaritySegment : RichSegment
{
    public string Text { get; }
    public string Rarity { get; }
    public RaritySegment(string text, string rarity) { Text = text; Rarity = rarity; }
}

public sealed class UrlSegment : RichSegment
{
    public string Url { get; }
    public UrlSegment(string url) => Url = url;
}

public sealed class EmoticonSegment : RichSegment
{
    public string Code { get; }
    public byte[]? Bytes { get; }
    public EmoticonSegment(string code, byte[]? bytes) { Code = code; Bytes = bytes; }
}

public sealed class ImageSegment : RichSegment
{
    public string Url { get; }
    public ImageSegment(string url) => Url = url;
}

public sealed class PlayerLinkSegment : RichSegment
{
    public string SteamId { get; }
    public string Url { get; }
    public string DisplayName { get; }
    public PlayerLinkSegment(string steamId, string url, string displayName)
    { SteamId = steamId; Url = url; DisplayName = displayName; }
}
