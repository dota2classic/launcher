using System.Collections.Generic;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.Models;

public sealed partial class ChatMessageView : ObservableObject
{
    public string MessageId { get; }

    [ObservableProperty]
    private string _content;
    public string AuthorName { get; }
    public string AuthorSteamId { get; }
    public string Initials { get; }
    public bool ShowHeader { get; }
    public Thickness GroupMargin => ShowHeader ? new Thickness(0, 6, 0, 0) : default;
    public string TimeText { get; }
    public string? AvatarUrl { get; }

    [ObservableProperty]
    private IReadOnlyList<RichSegment> _richContent;

    public ChatMessageView(
        string messageId,
        string content,
        IReadOnlyList<RichSegment> richContent,
        string authorName,
        string authorSteamId,
        bool showHeader,
        string timeText,
        string? avatarUrl = null)
    {
        MessageId = messageId;
        _content = content;
        _richContent = richContent;
        AuthorName = authorName;
        AuthorSteamId = authorSteamId;
        ShowHeader = showHeader;
        TimeText = timeText;
        AvatarUrl = avatarUrl;
        Initials = ComputeInitials(authorName);
    }

    private static string ComputeInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Trim().Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0].Length >= 2 ? parts[0][..2] : parts[0];
        return $"{parts[0][0]}{parts[^1][0]}";
    }
}
