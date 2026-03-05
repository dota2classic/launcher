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

namespace d2c_launcher.ViewModels;

public partial class ChatViewModel : ViewModelBase, IDisposable
{
    private const string ThreadId = "17aa3530-d152-462e-a032-909ae69019ed";
    private const int MessageLimit = 100;
    private const int MergeWindowSeconds = 60;

    private readonly IBackendApiService _backendApiService;
    private readonly Dictionary<string, string> _avatarUrlByAuthor = new(StringComparer.Ordinal);
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _sseCts;
    // Tracks the last appended message for SSE header-grouping decisions.
    private (string AuthorSteamId, string CreatedAt)? _lastMessageRaw;

    public Func<string?> GetBackendToken { get; set; } = () => null;

    public ObservableCollection<ChatMessageView> Messages { get; } = new();

    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSending;

    public event Action? MessagesUpdated;

    public ChatViewModel(IBackendApiService backendApiService)
    {
        _backendApiService = backendApiService;
    }

    /// <summary>Loads initial messages then starts the SSE live-update stream.</summary>
    public async Task StartAsync()
    {
        await RefreshAsync().ConfigureAwait(false);
        StartSseLoop();
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
            var token = GetBackendToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                try { await Task.Delay(5000, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            try
            {
                await foreach (var msg in _backendApiService.SubscribeChatAsync(ThreadId, token, ct))
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
        Dispatcher.UIThread.Post(() =>
        {
            if (msg.Deleted)
            {
                var existing = Messages.FirstOrDefault(m => m.MessageId == msg.MessageId);
                if (existing != null)
                    Messages.Remove(existing);
                return;
            }

            // Already present (e.g. loaded by initial fetch) — skip to avoid duplicates.
            if (Messages.Any(m => m.MessageId == msg.MessageId))
                return;

            var showHeader = _lastMessageRaw == null
                || _lastMessageRaw.Value.AuthorSteamId != msg.AuthorSteamId
                || Math.Abs((ParseDate(msg.CreatedAt) - ParseDate(_lastMessageRaw.Value.CreatedAt)).TotalSeconds)
                   > MergeWindowSeconds;

            if (!string.IsNullOrWhiteSpace(msg.AuthorAvatarUrl))
                _avatarUrlByAuthor[msg.AuthorSteamId] = msg.AuthorAvatarUrl!;

            var view = new ChatMessageView(
                msg.MessageId,
                msg.Content,
                msg.AuthorName,
                msg.AuthorSteamId,
                showHeader,
                FormatTime(ParseDate(msg.CreatedAt)));

            Messages.Add(view);
            _lastMessageRaw = (msg.AuthorSteamId, msg.CreatedAt);
            MessagesUpdated?.Invoke();

            if (showHeader)
                _ = LoadSingleAvatarAsync(view, CancellationToken.None);
        });
    }

    public async Task RefreshAsync()
    {
        var token = GetBackendToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            AppLog.Info("Chat: skipping refresh, no token.");
            return;
        }

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        try
        {
            Dispatcher.UIThread.Post(() => IsLoading = Messages.Count == 0);

            var data = await _backendApiService.GetChatMessagesAsync(
                ThreadId, MessageLimit, token, ct).ConfigureAwait(false);

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

            _ = LoadAvatarsAsync(grouped, ct);
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

        var token = GetBackendToken();
        if (string.IsNullOrWhiteSpace(token)) return;

        IsSending = true;
        var saved = text;
        try
        {
            InputText = "";
            await _backendApiService.PostChatMessageAsync(ThreadId, text, token)
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

            var showHeader = prev == null
                || prev.AuthorSteamId != msg.AuthorSteamId
                || Math.Abs((ParseDate(msg.CreatedAt) - ParseDate(prev.CreatedAt)).TotalSeconds)
                   > MergeWindowSeconds;

            if (!string.IsNullOrWhiteSpace(msg.AuthorAvatarUrl))
                _avatarUrlByAuthor[msg.AuthorSteamId] = msg.AuthorAvatarUrl!;

            result.Add(new ChatMessageView(
                msg.MessageId,
                msg.Content,
                msg.AuthorName,
                msg.AuthorSteamId,
                showHeader,
                FormatTime(ParseDate(msg.CreatedAt))));
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

    private async Task LoadAvatarsAsync(List<ChatMessageView> messages, CancellationToken ct)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var msg in messages)
        {
            if (!msg.ShowHeader) continue;
            if (!seen.Add(msg.AuthorSteamId)) continue;
            if (ct.IsCancellationRequested) return;
            await LoadSingleAvatarAsync(msg, ct).ConfigureAwait(false);
        }
    }

    private async Task LoadSingleAvatarAsync(ChatMessageView view, CancellationToken ct)
    {
        if (!_avatarUrlByAuthor.TryGetValue(view.AuthorSteamId, out var url)) return;
        var bitmap = await _backendApiService.LoadAvatarFromUrlAsync(url, ct).ConfigureAwait(false);
        if (bitmap == null || ct.IsCancellationRequested) return;

        Dispatcher.UIThread.Post(() =>
        {
            foreach (var m in Messages.Where(m => m.AuthorSteamId == view.AuthorSteamId))
                m.AvatarImage = bitmap;
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
        _sseCts?.Cancel();
        _sseCts?.Dispose();
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }
}
