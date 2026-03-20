using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using d2c_launcher.Models;
using d2c_launcher.ViewModels;
using Xunit;

namespace d2c_launcher.Tests;

/// <summary>
/// Tests for ChatMessageView — pure in-memory logic, no Avalonia platform needed
/// (Thickness is a value type; no renderer is initialised).
/// </summary>
public class ChatMessageViewTests
{
    // ── UpdateReactions ────────────────────────────────────────────────────────

    [Fact]
    public void UpdateReactions_EmptyToOne_AddsVmAndSetsHasReactions()
    {
        var view = MakeView();

        view.UpdateReactions(
            [new ChatReactionData(EmoticonId: 1, EmoticonCode: "wave", Count: 3, IsMine: false)],
            MakeReactionVm);

        Assert.Single(view.Reactions);
        Assert.Equal(1, view.Reactions[0].EmoticonId);
        Assert.Equal(3, view.Reactions[0].Count);
        Assert.False(view.Reactions[0].IsMine);
        Assert.True(view.HasReactions);
    }

    [Fact]
    public void UpdateReactions_ExistingReaction_UpdatesCountAndIsMineInPlace()
    {
        var view = MakeView();
        // Prime the collection with one reaction
        view.UpdateReactions(
            [new ChatReactionData(1, "wave", Count: 1, IsMine: false)],
            MakeReactionVm);
        var originalVm = view.Reactions[0];

        // Server pushes an updated count and flips IsMine
        view.UpdateReactions(
            [new ChatReactionData(1, "wave", Count: 5, IsMine: true)],
            MakeReactionVm);

        Assert.Single(view.Reactions);
        Assert.Same(originalVm, view.Reactions[0]);  // same VM instance — reused, not replaced
        Assert.Equal(5, view.Reactions[0].Count);
        Assert.True(view.Reactions[0].IsMine);
    }

    [Fact]
    public void UpdateReactions_ReactionDisappears_RemovedFromCollection()
    {
        var view = MakeView();
        view.UpdateReactions(
            [
                new ChatReactionData(1, "wave", Count: 2, IsMine: false),
                new ChatReactionData(2, "grin", Count: 1, IsMine: true),
            ],
            MakeReactionVm);

        // Emoticon 2 drops out
        view.UpdateReactions(
            [new ChatReactionData(1, "wave", Count: 2, IsMine: false)],
            MakeReactionVm);

        Assert.Single(view.Reactions);
        Assert.Equal(1, view.Reactions[0].EmoticonId);
    }

    [Fact]
    public void UpdateReactions_AllReactionsDisappear_HasReactionsBecomesFalse()
    {
        var view = MakeView();
        view.UpdateReactions(
            [new ChatReactionData(1, "wave", Count: 1, IsMine: false)],
            MakeReactionVm);
        Assert.True(view.HasReactions);

        view.UpdateReactions([], MakeReactionVm);

        Assert.Empty(view.Reactions);
        Assert.False(view.HasReactions);
    }

    [Fact]
    public void UpdateReactions_NewEmoticonAdded_FactoryCalledOnlyForNew()
    {
        var view = MakeView();
        view.UpdateReactions(
            [new ChatReactionData(1, "wave", Count: 1, IsMine: false)],
            MakeReactionVm);

        var factoryCalls = 0;
        view.UpdateReactions(
            [
                new ChatReactionData(1, "wave", Count: 2, IsMine: false),  // existing — factory not called
                new ChatReactionData(2, "grin", Count: 1, IsMine: true),   // new — factory called once
            ],
            data => { factoryCalls++; return MakeReactionVm(data); });

        Assert.Equal(1, factoryCalls);
        Assert.Equal(2, view.Reactions.Count);
    }

    // ── SetupQuickReacts ───────────────────────────────────────────────────────

    [Fact]
    public void SetupQuickReacts_FiveEmoticons_Top3InQuickReactsAllInPicker()
    {
        var view = MakeView();
        var top3 = new List<(int Id, string Code, byte[]? GifBytes)>
        {
            (1, "wave",  null),
            (2, "grin",  null),
            (3, "heart", null),
        };
        var all = new List<(int Id, string Code, byte[]? GifBytes)>
        {
            (1, "wave",  null),
            (2, "grin",  null),
            (3, "heart", null),
            (4, "fire",  null),
            (5, "100",   null),
        };

        view.SetupQuickReacts(top3, all, _ => Task.CompletedTask);

        Assert.Equal(3, view.QuickReacts.Count);
        Assert.Equal(5, view.AllEmoticonReacts.Count);
    }

    [Fact]
    public void SetupQuickReacts_TooltipFormattedWithColons()
    {
        var view = MakeView();
        var single = new List<(int Id, string Code, byte[]? GifBytes)> { (1, "wave", null) };

        view.SetupQuickReacts(single, single, _ => Task.CompletedTask);

        Assert.Equal(":wave:", view.QuickReacts[0].Tooltip);
    }

    [Fact]
    public void SetupQuickReacts_EmptyList_CollectionsClearedAndRemainEmpty()
    {
        var view = MakeView();
        // Prime with some entries first
        var one = new List<(int Id, string Code, byte[]? GifBytes)> { (1, "wave", null) };
        view.SetupQuickReacts(one, one, _ => Task.CompletedTask);

        // Now clear by passing empty lists
        view.SetupQuickReacts([], [], _ => Task.CompletedTask);

        Assert.Empty(view.QuickReacts);
        Assert.Empty(view.AllEmoticonReacts);
    }

    // ── Role flags ─────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_RoleFlagsPassedThrough()
    {
        var view = MakeView(isOld: true, isModerator: true, isAdmin: false);

        Assert.True(view.IsOld);
        Assert.True(view.IsModerator);
        Assert.False(view.IsAdmin);
    }

    [Fact]
    public void Constructor_ChatIconTooltip_FallsBackWhenTitleIsNull()
    {
        var view = MakeView(chatIconTitle: null);

        // Should not be null or empty — I18n returns the key or a localised string.
        Assert.False(string.IsNullOrEmpty(view.ChatIconTooltip));
    }

    [Fact]
    public void Constructor_ChatIconTooltip_UsesProvidedTitle()
    {
        var view = MakeView(chatIconTitle: "Gold Subscriber");

        Assert.Equal("Gold Subscriber", view.ChatIconTooltip);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ChatMessageView MakeView(
        bool isOld = false,
        bool isModerator = false,
        bool isAdmin = false,
        string? chatIconTitle = null)
        => new(
            messageId: "msg-1",
            content: "hello",
            richContent: Array.Empty<RichSegment>(),
            authorName: "TestUser",
            authorSteamId: "76561198000000001",
            showHeader: true,
            timeText: "12:00",
            createdAt: DateTimeOffset.UtcNow.ToString("o"),
            isOld: isOld,
            isModerator: isModerator,
            isAdmin: isAdmin,
            chatIconTitle: chatIconTitle);

    private static ChatReactionViewModel MakeReactionVm(ChatReactionData data)
        => new(data.EmoticonId, emoticonBytes: null, data.Count, data.IsMine, () => Task.CompletedTask);
}
