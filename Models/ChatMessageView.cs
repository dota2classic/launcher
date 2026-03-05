using System.Collections.Generic;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.Models;

public sealed partial class ChatMessageView : ObservableObject
{
    public string MessageId { get; }
    public string Content { get; }
    public string AuthorName { get; }
    public string AuthorSteamId { get; }
    public string Initials { get; }
    public bool ShowHeader { get; }
    public Thickness GroupMargin => ShowHeader ? new Thickness(0, 6, 0, 0) : default;
    public string TimeText { get; }

    [ObservableProperty]
    private IReadOnlyList<RichSegment> _richContent;

    [ObservableProperty]
    private Bitmap? _avatarImage;

    public ChatMessageView(
        string messageId,
        string content,
        IReadOnlyList<RichSegment> richContent,
        string authorName,
        string authorSteamId,
        bool showHeader,
        string timeText,
        Bitmap? avatarImage = null)
    {
        MessageId = messageId;
        Content = content;
        _richContent = richContent;
        AuthorName = authorName;
        AuthorSteamId = authorSteamId;
        ShowHeader = showHeader;
        TimeText = timeText;
        _avatarImage = avatarImage;
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
