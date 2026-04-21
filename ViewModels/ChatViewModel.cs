using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Resources;
using d2c_launcher.Util;
using static d2c_launcher.Util.ChatGrouper;

namespace d2c_launcher.ViewModels;

public partial class ChatViewModel : ViewModelBase, IDisposable
{
    private readonly string _threadId;
    private const int MessageLimit = 100;

    private readonly IBackendApiService _backendApiService;
    private readonly IHttpImageService _imageService;
    private readonly IEmoticonSnapshotBuilder _emoticonSnapshot;
    private readonly IUserNameResolver _userNameResolver;
    private readonly IChatMessageStream _messageStream;
    private readonly IQueueSocketService _queueSocketService;
    private readonly IWindowService _windowService;
    private readonly IUiDispatcher _dispatcher;

    private CancellationTokenSource? _loadCts;

    // Tracks the last appended message for SSE header-grouping decisions.
    private (string AuthorSteamId, string CreatedAt)? _lastMessageRaw;
    private HashSet<string> _onlineUsers = new(StringComparer.Ordinal);

    public ObservableCollection<ChatMessageView> Messages { get; } = new();
    public ObservableCollection<ChatQuickReactViewModel> InputEmoticonPicker { get; } = new();

    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSending;

    [ObservableProperty] private bool _isReplying;
    [ObservableProperty] private string? _replyTargetId;
    [ObservableProperty] private string? _replyTargetAuthor;
    [ObservableProperty] private string? _replyTargetContent;

    public event Action? MessagesUpdated;

    /// <summary>Set by the parent ViewModel to navigate to a player's profile. Receives steam32 ID.</summary>
    public Action<string>? OpenPlayerProfile { get; set; }

    [RelayCommand]
    private void OpenPlayerProfileById(string steam32Id) => OpenPlayerProfile?.Invoke(steam32Id);

    public ChatViewModel(
        string threadId,
        IBackendApiService backendApiService,
        IHttpImageService imageService,
        IEmoticonSnapshotBuilder emoticonSnapshot,
        IUserNameResolver userNameResolver,
        IChatMessageStream messageStream,
        IQueueSocketService queueSocketService,
        IWindowService windowService,
        IUiDispatcher dispatcher)
    {
        _threadId = threadId;
        _backendApiService = backendApiService;
        _imageService = imageService;
        _emoticonSnapshot = emoticonSnapshot;
        _userNameResolver = userNameResolver;
        _messageStream = messageStream;
        _queueSocketService = queueSocketService;
        _windowService = windowService;
        _dispatcher = dispatcher;

        _emoticonSnapshot.SnapshotReady += OnSnapshotReady;
        _messageStream.MessageReceived += OnMessageReceived;
        _queueSocketService.OnlineUpdated += OnOnlineUpdated;
        _windowService.WindowShown += OnWindowShown;
    }

    private void OnOnlineUpdated(OnlineUpdateMessage msg) =>
        _dispatcher.Post(() => UpdateOnlineUsers(msg));

    private void OnWindowShown()
    {
        _ = RefreshAsync();
        _messageStream.Restart();
    }

    private void OnSnapshotReady()
    {
        // Emoticons finished loading — re-parse, wire quick-reacts, and patch reaction icons in one pass.
        // Already on the UI thread (EmoticonSnapshotBuilder fires via IUiDispatcher).
        foreach (var msg in Messages)
        {
            msg.RichContent = RichMessageParser.Parse(msg.Content, _emoticonSnapshot.Images, _userNameResolver.GetOrCreate);
            SetupQuickReacts(msg);
            foreach (var reaction in msg.Reactions)
            {
                if (reaction.EmoticonBytes == null && _emoticonSnapshot.Images.TryGetValue(reaction.EmoticonCode, out var bytes))
                    reaction.EmoticonBytes = bytes;
            }
        }
        PopulateInputEmoticonPicker();
    }

    private void OnMessageReceived(ChatMessageData msg) => ConsumeIncomingMessage(msg);

    private void UpdateOnlineUsers(OnlineUpdateMessage msg)
    {
        _onlineUsers = msg.Online?.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in Messages)
            if (m.ShowHeader)
                m.IsOnline = _onlineUsers.Contains(m.AuthorSteamId);
    }

    /// <summary>Loads initial messages then starts the SSE live-update stream.</summary>
    public async Task StartAsync()
    {
        // Load emoticons in the background — don't block message loading.
        _ = _emoticonSnapshot.EnsureLoadedAsync();
        await RefreshAsync().ConfigureAwait(false);
        _messageStream.Start();
    }

    /// <summary>Cancels the current SSE connection and reconnects. Call when the auth token changes.</summary>
    public void RestartSse() => _messageStream.Restart();

    private void ConsumeIncomingMessage(ChatMessageData msg)
    {
        if (!_windowService.IsWindowVisible)
            return;

        _dispatcher.Post(() =>
        {
            if (msg.Deleted)
            {
                var existing = Messages.FirstOrDefault(m => m.MessageId == msg.MessageId);
                if (existing == null) return;

                var idx = Messages.IndexOf(existing);
                var wasHeader = existing.ShowHeader;
                Messages.Remove(existing);

                var recomputeAt = ChatGrouper.GetIndexToRecompute(idx, wasHeader, Messages.Count);
                if (recomputeAt >= 0)
                {
                    var next = Messages[recomputeAt];
                    var prev = recomputeAt > 0 ? Messages[recomputeAt - 1] : null;
                    var shouldBeHeader = ChatGrouper.ShouldShowHeader(
                        prev == null ? null : new ChatEntry(prev.AuthorSteamId, prev.CreatedAt),
                        new ChatEntry(next.AuthorSteamId, next.CreatedAt));
                    next.ShowHeader = shouldBeHeader;
                    if (shouldBeHeader)
                    {
                        next.AvatarUrl = next.AuthorAvatarUrl;
                        next.IsOnline = _onlineUsers.Contains(next.AuthorSteamId);
                    }
                }
                return;
            }

            // Already present — update content and reactions in case the message was edited or reacted to.
            var duplicate = Messages.FirstOrDefault(m => m.MessageId == msg.MessageId);
            if (duplicate != null)
            {
                duplicate.Content = msg.Content;
                duplicate.RichContent = RichMessageParser.Parse(msg.Content, _emoticonSnapshot.Images, _userNameResolver.GetOrCreate);
                if (msg.Reactions != null)
                    duplicate.UpdateReactions(msg.Reactions, data => BuildReactionVm(msg.MessageId, data));
                return;
            }

            var prevEntry = _lastMessageRaw == null ? null
                : new ChatEntry(_lastMessageRaw.Value.AuthorSteamId, _lastMessageRaw.Value.CreatedAt);
            var showHeader = ChatGrouper.ShouldShowHeader(prevEntry, new ChatEntry(msg.AuthorSteamId, msg.CreatedAt));

            var richContent = RichMessageParser.Parse(msg.Content, _emoticonSnapshot.Images, _userNameResolver.GetOrCreate);
            var view = new ChatMessageView(
                msg.MessageId,
                msg.Content,
                richContent,
                msg.AuthorName,
                msg.AuthorSteamId,
                showHeader,
                FormatTime(ParseDate(msg.CreatedAt)),
                msg.CreatedAt,
                msg.AuthorAvatarUrl,
                msg.ReplyToAuthorName,
                msg.ReplyToContent,
                msg.IsOld,
                msg.IsModerator,
                msg.IsAdmin,
                msg.ChatIconTitle);

            if (msg.Reactions != null)
                view.UpdateReactions(msg.Reactions, data => BuildReactionVm(msg.MessageId, data));
            SetupQuickReacts(view);
            if (showHeader)
                view.IsOnline = _onlineUsers.Contains(msg.AuthorSteamId);
            if (msg.IsOld && msg.ChatIconUrl != null)
                _ = LoadChatIconAsync(view, msg.ChatIconUrl);
            Messages.Add(view);
            _lastMessageRaw = (msg.AuthorSteamId, msg.CreatedAt);
            MessagesUpdated?.Invoke();
        });
    }

    public async Task RefreshAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var ct = cts.Token;

        bool IsLatest() => ReferenceEquals(_loadCts, cts);

        try
        {
            _dispatcher.Post(() => { if (IsLatest()) IsLoading = Messages.Count == 0; });

            var data = await _backendApiService.GetChatMessagesAsync(
                _threadId, MessageLimit, ct).ConfigureAwait(false);

            AppLog.Info($"Chat: received {data.Count} messages from API.");

            if (ct.IsCancellationRequested) return;

            var grouped = BuildGroupedMessages(data);

            _dispatcher.Post(() =>
            {
                if (ct.IsCancellationRequested) return;
                ApplyMessages(grouped);
                MessagesUpdated?.Invoke();
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppLog.Error($"Chat refresh failed: {ex.Message}", ex);
        }
        finally
        {
            _dispatcher.Post(() => { if (IsLatest()) IsLoading = false; });
        }
    }

    /// <summary>Triggers a refresh if the message list is empty. Call on user-driven events (e.g. tab switch)
    /// to recover from a failed initial load.</summary>
    public void RefreshIfEmpty()
    {
        if (Messages.Count == 0)
            _ = RefreshAsync();
    }

    public void SetReplyTarget(ChatMessageView msg)
    {
        ReplyTargetId = msg.MessageId;
        ReplyTargetAuthor = msg.AuthorName;
        ReplyTargetContent = msg.Content;
        IsReplying = true;
    }

    [RelayCommand]
    private void CancelReply()
    {
        IsReplying = false;
        ReplyTargetId = null;
        ReplyTargetAuthor = null;
        ReplyTargetContent = null;
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text)) return;

        IsSending = true;
        var saved = text;
        var replyId = ReplyTargetId;
        try
        {
            InputText = "";
            await _backendApiService.PostChatMessageAsync(_threadId, text, replyId)
                .ConfigureAwait(false);
            // SSE will deliver the sent message — no manual refresh needed.
            _dispatcher.Post(CancelReply);
        }
        catch (Exception ex)
        {
            AppLog.Error("Chat send failed.", ex);
            _dispatcher.Post(() => InputText = saved);
        }
        finally
        {
            _dispatcher.Post(() => IsSending = false);
        }
    }

    // ── Message grouping ──────────────────────────────────────────────────────

    private List<ChatMessageView> BuildGroupedMessages(IReadOnlyList<ChatMessageData> data)
    {
        // API returns DESC — sort ASC for display; parse dates once to avoid double parsing per message.
        var sorted = data.OrderBy(m => ParseDate(m.CreatedAt)).ToList();
        var result = new List<ChatMessageView>(sorted.Count);

        for (var i = 0; i < sorted.Count; i++)
        {
            var msg = sorted[i];
            var prev = i > 0 ? sorted[i - 1] : null;

            var prevEntry = prev == null ? null : new ChatEntry(prev.AuthorSteamId, prev.CreatedAt);
            var showHeader = ChatGrouper.ShouldShowHeader(prevEntry, new ChatEntry(msg.AuthorSteamId, msg.CreatedAt));
            var parsedDate = ParseDate(msg.CreatedAt);

            var richContent = RichMessageParser.Parse(msg.Content, _emoticonSnapshot.Images, _userNameResolver.GetOrCreate);
            var view = new ChatMessageView(
                msg.MessageId,
                msg.Content,
                richContent,
                msg.AuthorName,
                msg.AuthorSteamId,
                showHeader,
                FormatTime(parsedDate),
                msg.CreatedAt,
                msg.AuthorAvatarUrl,
                msg.ReplyToAuthorName,
                msg.ReplyToContent,
                msg.IsOld,
                msg.IsModerator,
                msg.IsAdmin,
                msg.ChatIconTitle);
            if (msg.Reactions != null)
                view.UpdateReactions(msg.Reactions, data => BuildReactionVm(msg.MessageId, data));
            SetupQuickReacts(view);
            if (showHeader)
                view.IsOnline = _onlineUsers.Contains(msg.AuthorSteamId);
            if (msg.IsOld && msg.ChatIconUrl != null)
                _ = LoadChatIconAsync(view, msg.ChatIconUrl);
            result.Add(view);
        }

        // Remember last message so ConsumeIncomingMessage can compute headers for SSE events.
        if (sorted.Count > 0)
        {
            var last = sorted[^1];
            _lastMessageRaw = (last.AuthorSteamId, last.CreatedAt);
        }

        return result;
    }

    private void ApplyMessages(List<ChatMessageView> incoming)
    {
        if (Messages.Count == incoming.Count
            && Messages.Zip(incoming).All(p => p.First.MessageId == p.Second.MessageId))
            return;

        Messages.Clear();
        foreach (var m in incoming)
            Messages.Add(m);
    }

    // ── Reactions ─────────────────────────────────────────────────────────────

    private void PopulateInputEmoticonPicker()
    {
        InputEmoticonPicker.Clear();
        foreach (var (id, code, gifBytes) in _emoticonSnapshot.All)
            InputEmoticonPicker.Add(new ChatQuickReactViewModel(id, code, gifBytes, () => Task.CompletedTask));
    }

    private void SetupQuickReacts(ChatMessageView view)
    {
        if (!_emoticonSnapshot.IsLoaded) return;
        var messageId = view.MessageId;
        view.SetupQuickReacts(_emoticonSnapshot.Top3, _emoticonSnapshot.All, emoticonId => ReactToMessageAsync(messageId, emoticonId));
    }

    private ChatReactionViewModel BuildReactionVm(string messageId, ChatReactionData data)
    {
        _emoticonSnapshot.Images.TryGetValue(data.EmoticonCode, out var bytes);
        return new ChatReactionViewModel(
            data.EmoticonId,
            data.EmoticonCode,
            bytes,
            data.Count,
            data.IsMine,
            () => ReactToMessageAsync(messageId, data.EmoticonId));
    }

    private async Task ReactToMessageAsync(string messageId, int emoticonId)
    {
        try
        {
            var updatedReactions = await _backendApiService
                .ReactToMessageAsync(messageId, emoticonId)
                .ConfigureAwait(false);

            _dispatcher.Post(() =>
            {
                var view = Messages.FirstOrDefault(m => m.MessageId == messageId);
                view?.UpdateReactions(updatedReactions, data => BuildReactionVm(messageId, data));
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppLog.Error($"Chat: react to message {messageId} failed: {ex.Message}", ex);
        }
    }

    // ── Chat icon ─────────────────────────────────────────────────────────────

    private async Task LoadChatIconAsync(ChatMessageView view, string url)
    {
        try
        {
            var bytes = await _imageService.LoadBytesAsync(url).ConfigureAwait(false);
            if (bytes != null)
                _dispatcher.Post(() => view.ChatIconBytes = bytes);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Chat: failed to load chat icon from {url}: {ex.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatTime(DateTimeOffset dt)
    {
        if (dt == DateTimeOffset.MinValue) return "";
        var local = dt.ToLocalTime();
        return local.Date == DateTime.Today
            ? I18n.T("chat.todayAt", ("time", $"{local:HH:mm}"))
            : $"{local.Day} {RuMonth(local.Month)} {local:HH:mm}";
    }

    private static string RuMonth(int m) => m switch
    {
        1 => Strings.MonthJan, 2 => Strings.MonthFeb, 3 => Strings.MonthMar, 4 => Strings.MonthApr,
        5 => Strings.MonthMay, 6 => Strings.MonthJun, 7 => Strings.MonthJul, 8 => Strings.MonthAug,
        9 => Strings.MonthSep, 10 => Strings.MonthOct, 11 => Strings.MonthNov, _ => Strings.MonthDec
    };

    public void Dispose()
    {
        _emoticonSnapshot.SnapshotReady -= OnSnapshotReady;
        _messageStream.MessageReceived -= OnMessageReceived;
        _queueSocketService.OnlineUpdated -= OnOnlineUpdated;
        _windowService.WindowShown -= OnWindowShown;
        _messageStream.Dispose();
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }
}
