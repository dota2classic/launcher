using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Util;
using static d2c_launcher.Util.ChatGrouper;

namespace d2c_launcher.ViewModels;

public partial class ChatViewModel : ViewModelBase, IDisposable
{
    private readonly string _threadId;
    private const int MessageLimit = 100;
    private const int MergeWindowSeconds = 60;

    private readonly IBackendApiService _backendApiService;
    private readonly IHttpImageService _imageService;
    private readonly IEmoticonService _emoticonService;
    // Emoticon GIF bytes keyed by code (populated once at startup).
    private Dictionary<string, byte[]> _emoticonImages = new(StringComparer.Ordinal);
    // User name cache: steamId → resolved name (null = fetch in-flight).
    private readonly Dictionary<string, string?> _userNameCache = new(StringComparer.Ordinal);
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _sseCts;
    // Tracks the last appended message for SSE header-grouping decisions.
    private (string AuthorSteamId, string CreatedAt)? _lastMessageRaw;
    private HashSet<string> _onlineUsers = new(StringComparer.Ordinal);

    public ObservableCollection<ChatMessageView> Messages { get; } = new();

    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSending;

    public event Action? MessagesUpdated;

    private readonly IWindowService _windowService;

    public ChatViewModel(string threadId, IBackendApiService backendApiService, IHttpImageService imageService, IEmoticonService emoticonService, IQueueSocketService queueSocketService, IWindowService windowService)
    {
        _threadId = threadId;
        _backendApiService = backendApiService;
        _imageService = imageService;
        _emoticonService = emoticonService;
        _windowService = windowService;
        queueSocketService.OnlineUpdated += msg => Dispatcher.UIThread.Post(() => UpdateOnlineUsers(msg));
        windowService.WindowShown += OnWindowShown;
    }

    private void OnWindowShown()
    {
        _ = RefreshAsync();
        RestartSse();
    }

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
        // Initial messages will render without emoticon images; SSE messages
        // arriving after emoticons finish will have them.
        _ = LoadEmoticonsAsync();
        await RefreshAsync().ConfigureAwait(false);
        StartSseLoop();
    }

    private async Task LoadEmoticonsAsync()
    {
        try
        {
            _emoticonImages = await _emoticonService.GetEmoticonImagesAsync().ConfigureAwait(false);

            // Re-parse any messages that were rendered before emoticons finished loading.
            Dispatcher.UIThread.Post(ReparseAllMessages);
        }
        catch (Exception ex)
        {
            AppLog.Error($"Chat: failed to load emoticons: {ex.Message}", ex);
        }
    }

    private void ReparseAllMessages()
    {
        foreach (var msg in Messages)
            msg.RichContent = RichMessageParser.Parse(msg.Content, _emoticonImages, _userNameCache);
    }

    /// <summary>Cancels the current SSE connection and reconnects. Call when the auth token changes.</summary>
    public void RestartSse() => StartSseLoop();

    private void StartSseLoop()
    {
        _sseCts?.Cancel();
        _sseCts?.Dispose();
        _sseCts = new CancellationTokenSource();
        _ = RunSseLoopAsync(_sseCts.Token);
    }

    private async Task RunSseLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await foreach (var msg in _backendApiService.SubscribeChatAsync(_threadId, ct))
                    ConsumeIncomingMessage(msg);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLog.Error($"Chat SSE disconnected: {ex.Message}", ex);
                try { await Task.Delay(3000, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private void ConsumeIncomingMessage(Models.ChatMessageData msg)
    {
        if (!_windowService.IsWindowVisible)
            return;

        Dispatcher.UIThread.Post(() =>
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

            // Already present — update content in case the message was edited.
            var duplicate = Messages.FirstOrDefault(m => m.MessageId == msg.MessageId);
            if (duplicate != null)
            {
                duplicate.Content = msg.Content;
                duplicate.RichContent = RichMessageParser.Parse(msg.Content, _emoticonImages, _userNameCache);
                return;
            }

            var prevEntry = _lastMessageRaw == null ? null
                : new ChatEntry(_lastMessageRaw.Value.AuthorSteamId, _lastMessageRaw.Value.CreatedAt);
            var showHeader = ChatGrouper.ShouldShowHeader(prevEntry, new ChatEntry(msg.AuthorSteamId, msg.CreatedAt));

            var richContent = RichMessageParser.Parse(msg.Content, _emoticonImages, _userNameCache);
            ScheduleUserLoads(richContent);
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
                msg.ReplyToContent);

            if (showHeader)
                view.IsOnline = _onlineUsers.Contains(msg.AuthorSteamId);
            Messages.Add(view);
            _lastMessageRaw = (msg.AuthorSteamId, msg.CreatedAt);
            MessagesUpdated?.Invoke();
        });
    }

    public async Task RefreshAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        try
        {
            Dispatcher.UIThread.Post(() => IsLoading = Messages.Count == 0);

            var data = await _backendApiService.GetChatMessagesAsync(
                _threadId, MessageLimit, ct).ConfigureAwait(false);

            AppLog.Info($"Chat: received {data.Count} messages from API.");

            if (ct.IsCancellationRequested) return;

            var grouped = BuildGroupedMessages(data);

            Dispatcher.UIThread.Post(() =>
            {
                if (ct.IsCancellationRequested) return;
                ApplyMessages(grouped);
                MessagesUpdated?.Invoke();
                IsLoading = false;
            });

            }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppLog.Error($"Chat refresh failed: {ex.Message}", ex);
            Dispatcher.UIThread.Post(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text)) return;

        IsSending = true;
        var saved = text;
        try
        {
            InputText = "";
            await _backendApiService.PostChatMessageAsync(_threadId, text)
                .ConfigureAwait(false);
            // SSE will deliver the sent message — no manual refresh needed.
        }
        catch (Exception ex)
        {
            AppLog.Error("Chat send failed.", ex);
            Dispatcher.UIThread.Post(() => InputText = saved);
        }
        finally
        {
            IsSending = false;
        }
    }

    // ── Message grouping ──────────────────────────────────────────────────────

    private List<ChatMessageView> BuildGroupedMessages(IReadOnlyList<ChatMessageData> data)
    {
        // API returns DESC — sort ASC for display
        var sorted = data.OrderBy(m => ParseDate(m.CreatedAt)).ToList();
        var result = new List<ChatMessageView>(sorted.Count);

        for (var i = 0; i < sorted.Count; i++)
        {
            var msg = sorted[i];
            var prev = i > 0 ? sorted[i - 1] : null;

            var prevEntry = prev == null ? null : new ChatEntry(prev.AuthorSteamId, prev.CreatedAt);
            var showHeader = ChatGrouper.ShouldShowHeader(prevEntry, new ChatEntry(msg.AuthorSteamId, msg.CreatedAt));

            var richContent = RichMessageParser.Parse(msg.Content, _emoticonImages, _userNameCache);
            ScheduleUserLoads(richContent);
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
                msg.ReplyToContent);
            if (showHeader)
                view.IsOnline = _onlineUsers.Contains(msg.AuthorSteamId);
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

    // ── Player name resolution ────────────────────────────────────────────────

    private void ScheduleUserLoads(IReadOnlyList<RichSegment> segments)
    {
        foreach (var seg in segments.OfType<PlayerLinkSegment>())
        {
            if (_userNameCache.ContainsKey(seg.SteamId)) continue;
            _userNameCache[seg.SteamId] = null; // mark as in-flight
            _ = LoadUserAsync(seg.SteamId);
        }
    }

    private async Task LoadUserAsync(string steamId)
    {
        var info = await _backendApiService.GetUserInfoAsync(steamId).ConfigureAwait(false);
        if (info == null)
        {
            // No token yet or user not found — remove from cache so it retries next time.
            _userNameCache.Remove(steamId);
            return;
        }
        var name = info.Value.Name ?? steamId;

        Dispatcher.UIThread.Post(() =>
        {
            _userNameCache[steamId] = name;
            ReparseAllMessages();
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DateTimeOffset ParseDate(string iso)
    {
        if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            return dt;
        return DateTimeOffset.MinValue;
    }

    private static string FormatTime(DateTimeOffset dt)
    {
        if (dt == DateTimeOffset.MinValue) return "";
        var local = dt.ToLocalTime();
        return local.Date == DateTime.Today
            ? $"Сегодня в {local:HH:mm}"
            : $"{local.Day} {RuMonth(local.Month)} {local:HH:mm}";
    }

    private static string RuMonth(int m) => m switch
    {
        1 => "янв.", 2 => "фев.", 3 => "мар.", 4 => "апр.",
        5 => "мая",  6 => "июн.", 7 => "июл.", 8 => "авг.",
        9 => "сен.", 10 => "окт.", 11 => "ноя.", _ => "дек."
    };

    public void Dispose()
    {
        _windowService.WindowShown -= OnWindowShown;
        _sseCts?.Cancel();
        _sseCts?.Dispose();
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }
}
