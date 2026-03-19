using System.Collections.Generic;

namespace d2c_launcher.Models;

public record ChatMessageData(
    string MessageId,
    string ThreadId,
    string Content,
    string CreatedAt,
    string AuthorSteamId,
    string AuthorName,
    string? AuthorAvatarUrl,
    bool Deleted,
    string? ReplyToAuthorName = null,
    string? ReplyToContent = null,
    IReadOnlyList<ChatReactionData>? Reactions = null);
