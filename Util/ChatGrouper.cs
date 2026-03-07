using System;
using System.Collections.Generic;
using System.Globalization;

namespace d2c_launcher.Util;

/// <summary>
/// Pure grouping logic for chat messages — no Avalonia or ViewModel dependencies.
/// </summary>
public static class ChatGrouper
{
    public const int MergeWindowSeconds = 60;

    /// <summary>
    /// Returns true if <paramref name="current"/> should render a header
    /// (avatar + name + timestamp) given the immediately preceding message.
    /// </summary>
    public static bool ShouldShowHeader(ChatEntry? prev, ChatEntry current)
    {
        if (prev == null) return true;
        if (prev.AuthorSteamId != current.AuthorSteamId) return true;
        return Math.Abs((ParseDate(current.CreatedAt) - ParseDate(prev.CreatedAt)).TotalSeconds)
               > MergeWindowSeconds;
    }

    /// <summary>
    /// After a message is removed from the list at <paramref name="deletedIndex"/>,
    /// returns the index of the message that must have its ShowHeader re-evaluated,
    /// or -1 if no update is needed.
    /// <para>
    /// A re-evaluation is needed only when the deleted message was itself a header,
    /// because the message that follows it may have been rendered as inline (no header)
    /// but must now become a header.
    /// </para>
    /// </summary>
    public static int GetIndexToRecompute(int deletedIndex, bool deletedShowedHeader, int newCount)
    {
        if (!deletedShowedHeader) return -1;
        if (deletedIndex >= newCount) return -1;
        return deletedIndex;
    }

    public static DateTimeOffset ParseDate(string iso)
    {
        if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            return dt;
        return DateTimeOffset.MinValue;
    }
}

/// <summary>Minimal data needed by <see cref="ChatGrouper"/> — no UI dependencies.</summary>
public sealed record ChatEntry(string AuthorSteamId, string CreatedAt);
