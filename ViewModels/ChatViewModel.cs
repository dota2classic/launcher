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
    private readonly DispatcherTimer _refreshTimer;
    private readonly Dictionary<string, string> _avatarUrlByAuthor = new(StringComparer.Ordinal);
    private CancellationTokenSource? _loadCts;
    private int _refreshRunning;

    public Func<string?> GetBackendToken { get; set; } = () => null;

    public ObservableCollection<ChatMessageView> Messages { get; } = new();

    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSending;

    public event Action? MessagesUpdated;

    public ChatViewModel(IBackendApiService backendApiService)
    {
        _backendApiService = backendApiService;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _refreshTimer.Tick += (_, _) => { _ = RefreshAsync(); };
        _refreshTimer.Start();
    }

    public async Task RefreshAsync()
    {
        if (Interlocked.Exchange(ref _refreshRunning, 1) != 0)
            return;

        var token = GetBackendToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            AppLog.Info("Chat: skipping refresh, no token.");
            Interlocked.Exchange(ref _refreshRunning, 0);
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
        finally
        {
            Interlocked.Exchange(ref _refreshRunning, 0);
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
            await RefreshAsync().ConfigureAwait(false);
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
            if (!_avatarUrlByAuthor.TryGetValue(msg.AuthorSteamId, out var url)) continue;

            if (ct.IsCancellationRequested) return;
            var bitmap = await _backendApiService.LoadAvatarFromUrlAsync(url, ct)
                .ConfigureAwait(false);
            if (bitmap == null || ct.IsCancellationRequested) continue;

            Dispatcher.UIThread.Post(() =>
            {
                foreach (var m in Messages.Where(m => m.AuthorSteamId == msg.AuthorSteamId))
                    m.AvatarImage = bitmap;
            });
        }
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
        _refreshTimer.Stop();
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }
}
