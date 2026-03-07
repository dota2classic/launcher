using System;
using System.Collections.Generic;
using d2c_launcher.Util;
using Xunit;

namespace d2c_launcher.Tests;

/// <summary>
/// Tests for ChatGrouper — pure header-grouping logic, no Avalonia/ViewModel deps.
/// </summary>
public class ChatGrouperTests
{
    // ── ShouldShowHeader ──────────────────────────────────────────────────────

    [Fact]
    public void ShouldShowHeader_NoPrev_ReturnsTrue()
    {
        var result = ChatGrouper.ShouldShowHeader(null, Entry("alice", Ago(0)));
        Assert.True(result);
    }

    [Fact]
    public void ShouldShowHeader_SameAuthorWithinWindow_ReturnsFalse()
    {
        var prev = Entry("alice", Ago(30));
        var curr = Entry("alice", Ago(0));
        Assert.False(ChatGrouper.ShouldShowHeader(prev, curr));
    }

    [Fact]
    public void ShouldShowHeader_SameAuthorExceedsWindow_ReturnsTrue()
    {
        var prev = Entry("alice", Ago(61));
        var curr = Entry("alice", Ago(0));
        Assert.True(ChatGrouper.ShouldShowHeader(prev, curr));
    }

    [Fact]
    public void ShouldShowHeader_DifferentAuthor_ReturnsTrue()
    {
        var prev = Entry("alice", Ago(5));
        var curr = Entry("bob", Ago(0));
        Assert.True(ChatGrouper.ShouldShowHeader(prev, curr));
    }

    // ── GetIndexToRecompute ───────────────────────────────────────────────────

    [Fact]
    public void GetIndexToRecompute_DeletedWasNotHeader_ReturnsMinusOne()
    {
        // Non-header deleted: successor needs no change.
        var idx = ChatGrouper.GetIndexToRecompute(deletedIndex: 1, deletedShowedHeader: false, newCount: 2);
        Assert.Equal(-1, idx);
    }

    [Fact]
    public void GetIndexToRecompute_DeletedWasLastMessage_ReturnsMinusOne()
    {
        // Header deleted, but nothing follows it.
        var idx = ChatGrouper.GetIndexToRecompute(deletedIndex: 2, deletedShowedHeader: true, newCount: 2);
        Assert.Equal(-1, idx);
    }

    [Fact]
    public void GetIndexToRecompute_DeletedWasHeader_ReturnsSuccessorIndex()
    {
        var idx = ChatGrouper.GetIndexToRecompute(deletedIndex: 0, deletedShowedHeader: true, newCount: 2);
        Assert.Equal(0, idx);
    }

    // ── End-to-end deletion scenarios ─────────────────────────────────────────

    [Fact]
    public void DeleteHeader_SuccessorSameAuthor_SuccessorBecomesHeader()
    {
        // [alice-header, alice-inline, alice-inline]
        // Delete alice-header → alice-inline at [0] should become header.
        var messages = new List<TestMsg>
        {
            new("1", "alice", Ago(10), showHeader: true),
            new("2", "alice", Ago(8),  showHeader: false),
            new("3", "alice", Ago(5),  showHeader: false),
        };

        SimulateDeletion(messages, deletedId: "1");

        // Message "2" is now at index 0; prev = null → must be a header.
        Assert.True(messages[0].ShowHeader);
        Assert.Equal("2", messages[0].Id);
        // Message "3" stays inline under the new header.
        Assert.False(messages[1].ShowHeader);
    }

    [Fact]
    public void DeleteHeader_SuccessorDifferentAuthor_SuccessorStaysHeader()
    {
        // [alice-header, bob-header, charlie-header]
        // Delete alice-header → bob is already a header; must stay header.
        var messages = new List<TestMsg>
        {
            new("1", "alice", Ago(10), showHeader: true),
            new("2", "bob",   Ago(8),  showHeader: true),
            new("3", "charlie", Ago(5), showHeader: true),
        };

        SimulateDeletion(messages, deletedId: "1");

        Assert.True(messages[0].ShowHeader);  // bob stays a header
        Assert.True(messages[1].ShowHeader);  // charlie unchanged
    }

    [Fact]
    public void DeleteNonHeader_SuccessorUnchanged()
    {
        // [alice-header, alice-inline, alice-inline]
        // Delete alice-inline at index 1 → alice-inline at index 1 (was index 2) stays inline.
        var messages = new List<TestMsg>
        {
            new("1", "alice", Ago(10), showHeader: true),
            new("2", "alice", Ago(8),  showHeader: false),
            new("3", "alice", Ago(5),  showHeader: false),
        };

        SimulateDeletion(messages, deletedId: "2");

        Assert.True(messages[0].ShowHeader);   // alice header unchanged
        Assert.False(messages[1].ShowHeader);  // "3" stays inline
    }

    [Fact]
    public void DeleteLastMessage_NoError()
    {
        var messages = new List<TestMsg>
        {
            new("1", "alice", Ago(10), showHeader: true),
            new("2", "alice", Ago(5),  showHeader: false),
        };

        SimulateDeletion(messages, deletedId: "2");

        Assert.Single(messages);
        Assert.True(messages[0].ShowHeader);
    }

    [Fact]
    public void DeleteHeader_MiddleOfList_PrevAndNextSameAuthor_SuccessorBecomesHeader()
    {
        // [bob-header, alice-header, alice-inline]
        // Delete alice-header → alice-inline at [1]; prev = bob → different author → header.
        var messages = new List<TestMsg>
        {
            new("1", "bob",   Ago(20), showHeader: true),
            new("2", "alice", Ago(10), showHeader: true),
            new("3", "alice", Ago(5),  showHeader: false),
        };

        SimulateDeletion(messages, deletedId: "2");

        Assert.True(messages[0].ShowHeader);  // bob unchanged
        Assert.True(messages[1].ShowHeader);  // "3" (alice) promoted
    }

    [Fact]
    public void DeleteHeader_SuccessorFromSameAuthorAsNewPrev_SuccessorMayMerge()
    {
        // [alice-header(t-20), alice-header(t-10), alice-inline(t-5)]
        // This can happen if time gap was >60s between first two, but <60s between second and third.
        // Delete alice-header at index 1 → alice-inline at [1]; prev = alice at [0], gap = 15s < 60s → inline.
        var messages = new List<TestMsg>
        {
            new("1", "alice", Ago(15), showHeader: true),
            new("2", "alice", Ago(10), showHeader: true),
            new("3", "alice", Ago(5),  showHeader: false),
        };

        SimulateDeletion(messages, deletedId: "2");

        Assert.True(messages[0].ShowHeader);   // "1" unchanged
        Assert.False(messages[1].ShowHeader);  // "3" merges under "1" (same author, 10s gap)
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string Ago(int secondsAgo)
        => DateTimeOffset.UtcNow.AddSeconds(-secondsAgo).ToString("o");

    private static ChatEntry Entry(string authorId, string createdAt)
        => new(authorId, createdAt);

    /// <summary>Simulates the deletion logic from ChatViewModel.ConsumeIncomingMessage.</summary>
    private static void SimulateDeletion(List<TestMsg> messages, string deletedId)
    {
        var existing = messages.Find(m => m.Id == deletedId);
        if (existing == null) return;

        var idx = messages.IndexOf(existing);
        var wasHeader = existing.ShowHeader;
        messages.Remove(existing);

        var recomputeAt = ChatGrouper.GetIndexToRecompute(idx, wasHeader, messages.Count);
        if (recomputeAt < 0) return;

        var next = messages[recomputeAt];
        var prev = recomputeAt > 0 ? messages[recomputeAt - 1] : null;
        next.ShowHeader = ChatGrouper.ShouldShowHeader(
            prev == null ? null : new ChatEntry(prev.AuthorSteamId, prev.CreatedAt),
            new ChatEntry(next.AuthorSteamId, next.CreatedAt));
    }

    private sealed class TestMsg
    {
        public string Id { get; }
        public string AuthorSteamId { get; }
        public string CreatedAt { get; }
        public bool ShowHeader { get; set; }

        public TestMsg(string id, string authorSteamId, string createdAt, bool showHeader)
        {
            Id = id;
            AuthorSteamId = authorSteamId;
            CreatedAt = createdAt;
            ShowHeader = showHeader;
        }
    }
}
