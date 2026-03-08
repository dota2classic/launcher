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
    public string TimeText { get; }
    /// <summary>Raw ISO timestamp — used for grouping re-computation after deletion.</summary>
    public string CreatedAt { get; }
    /// <summary>Avatar URL always stored (even for inline messages) so deletion can promote a successor.</summary>
    public string? AuthorAvatarUrl { get; }

    [ObservableProperty]
    private bool _showHeader;

    [ObservableProperty]
    private bool _isOnline;

    [ObservableProperty]
    private string? _avatarUrl;

    public Thickness GroupMargin => ShowHeader ? new Thickness(0, 6, 0, 0) : default;

    partial void OnShowHeaderChanged(bool value) => OnPropertyChanged(nameof(GroupMargin));

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
        string createdAt,
        string? authorAvatarUrl = null)
    {
        MessageId = messageId;
        _content = content;
        _richContent = richContent;
        AuthorName = authorName;
        AuthorSteamId = authorSteamId;
        _showHeader = showHeader;
        TimeText = timeText;
        CreatedAt = createdAt;
        AuthorAvatarUrl = authorAvatarUrl;
        _avatarUrl = showHeader ? authorAvatarUrl : null;
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
