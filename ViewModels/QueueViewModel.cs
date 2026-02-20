using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Api;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class QueueViewModel : ViewModelBase, IDisposable
{
    // Static brushes — allocated once; avoid new allocations on every property read.
    private static readonly IBrush BrushReady         = new SolidColorBrush(Color.Parse("#1A5276"));
    private static readonly IBrush BrushSearching     = new SolidColorBrush(Color.Parse("#27AE60"));
    private static readonly IBrush BrushIdle          = new SolidColorBrush(Color.Parse("#1F8B4C"));
    private static readonly IBrush BrushReadyHover    = new SolidColorBrush(Color.Parse("#2E86C1"));
    private static readonly IBrush BrushSearchingHover = new SolidColorBrush(Color.Parse("#2ECC71"));
    private static readonly IBrush BrushIdleHover     = new SolidColorBrush(Color.Parse("#27AE60"));
    private readonly IQueueSocketService _queueSocketService;
    private readonly IBackendApiService _backendApiService;
    private readonly DispatcherTimer _queueTimer;
    private DateTimeOffset? _enterQueueAt;
    private int _queuedModeCount;
    private bool _hasServerUrl;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _searchingModesText = "Не в поиске";

    [ObservableProperty]
    private ObservableCollection<MatchmakingModeView> _matchmakingModes = new();

    [ObservableProperty]
    private string _queueButtonMainText = "ИГРАТЬ";

    [ObservableProperty]
    private string _queueButtonModeCountText = "";

    [ObservableProperty]
    private string _queueButtonTimeText = "";

    /// <summary>Blue when game ready, green when searching, dark gray when idle.</summary>
    public IBrush QueueButtonBackground => _hasServerUrl ? BrushReady
        : IsSearching ? BrushSearching : BrushIdle;

    /// <summary>Lighter version for hover state.</summary>
    public IBrush QueueButtonHoverBackground => _hasServerUrl ? BrushReadyHover
        : IsSearching ? BrushSearchingHover : BrushIdleHover;

    public QueueViewModel(IQueueSocketService queueSocketService, IBackendApiService backendApiService)
    {
        _queueSocketService = queueSocketService;
        _backendApiService = backendApiService;

        _queueTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _queueTimer.Tick += (_, _) => UpdateQueueButtonState();
        _queueTimer.Start();

        queueSocketService.QueueStateUpdated += msg =>
            Dispatcher.UIThread.Post(() => UpdateQueueCounts(msg));
        queueSocketService.PlayerQueueStateUpdated += msg =>
            Dispatcher.UIThread.Post(() => UpdatePlayerQueueState(msg));
        queueSocketService.PlayerGameStateUpdated += msg =>
            Dispatcher.UIThread.Post(() =>
            {
                _hasServerUrl = !string.IsNullOrEmpty(msg?.ServerUrl);
                UpdateQueueButtonState();
            });

        _ = RefreshMatchmakingModesAsync();
        UpdateQueueButtonState();
    }

    public async Task ToggleSearchAsync()
    {
        if (IsSearching)
        {
            await _queueSocketService.LeaveAllQueuesAsync();
            return;
        }

        var selected = MatchmakingModes.Where(m => m.IsSelected)
            .Select(m => (MatchmakingMode)m.ModeId)
            .ToArray();
        if (selected.Length == 0)
            return;

        await _queueSocketService.EnterQueueAsync(selected);
    }

    public async Task RefreshMatchmakingModesAsync()
    {
        try
        {
            var modes = await _backendApiService.GetEnabledMatchmakingModesAsync();
            var next = new ObservableCollection<MatchmakingModeView>();

            foreach (var mode in modes)
            {
                var existing = MatchmakingModes.FirstOrDefault(m => m.ModeId == mode.ModeId);
                var view = new MatchmakingModeView(mode.ModeId, mode.Name, existing?.IsSelected ?? false)
                {
                    InQueue = existing?.InQueue ?? 0
                };
                next.Add(view);
            }

            MatchmakingModes = new ObservableCollection<MatchmakingModeView>(
                next.OrderBy(m => GetModePriority(m.ModeId)));
            ApplyRestrictionsToModes();
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to load matchmaking modes.", ex);
        }
    }

    private void UpdateQueueCounts(QueueStateMessage msg)
    {
        var id = (int)msg.Mode;
        foreach (var mode in MatchmakingModes)
        {
            if (mode.ModeId == id)
            {
                mode.InQueue = msg.InQueue;
                break;
            }
        }
    }

    private void UpdatePlayerQueueState(PlayerQueueStateMessage msg)
    {
        if (msg.InQueue)
        {
            _queuedModeCount = msg.Modes?.Length ?? 0;
            var names = MatchmakingModes
                .Where(m => msg.Modes.Any(x => (int)x == m.ModeId))
                .Select(m => m.Name)
                .ToArray();
            SearchingModesText = names.Length > 0
                ? $"В поиске: {string.Join(", ", names)}"
                : "В поиске";
        }
        else
        {
            _queuedModeCount = 0;
            SearchingModesText = "Не в поиске";
        }

        IsSearching = msg.InQueue;
        UpdateQueueButtonState();
    }

    public void UpdateQueueButtonState()
    {
        if (_hasServerUrl)
        {
            QueueButtonMainText = "ПОДКЛЮЧИТЬСЯ";
            QueueButtonModeCountText = "";
            QueueButtonTimeText = "";
        }
        else if (IsSearching)
        {
            QueueButtonMainText = "ОТМЕНИТЬ ПОИСК";
            QueueButtonModeCountText = _queuedModeCount > 0 ? FormatModeCount(_queuedModeCount) : "";
            var elapsed = _enterQueueAt.HasValue
                ? DateTimeOffset.UtcNow.Subtract(_enterQueueAt.Value.UtcDateTime)
                : TimeSpan.Zero;
            QueueButtonTimeText = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
        }
        else
        {
            QueueButtonMainText = "ИГРАТЬ";
            QueueButtonModeCountText = "";
            QueueButtonTimeText = "";
        }
        OnPropertyChanged(nameof(QueueButtonBackground));
        OnPropertyChanged(nameof(QueueButtonHoverBackground));
    }

    public void SetEnterQueueAt(DateTimeOffset? time) => _enterQueueAt = time;

    // Priority mirrors getLobbyTypePriority from the web client (lower = shown first)
    private static int GetModePriority(int modeId) => modeId + modeId switch
    {
        1  => -1000,  // UNRANKED   (Обычная 5x5)
        8  => -1500,  // HIGHROOM   (Highroom 5x5)
        12 => -100,   // BOTS2X2    (2x2 с ботами)
        13 => -500,   // TURBO      (Турбо)
        _  => 0
    };

    // Mode ID → access category
    private static readonly System.Collections.Generic.HashSet<int> HumanGameModeIds =
        new() { 0, 1, 3, 4, 5, 6, 8, 9, 10, 11 };
    private static readonly System.Collections.Generic.HashSet<int> SimpleModeIds =
        new() { 2, 13 };
    private static readonly System.Collections.Generic.HashSet<int> EducationModeIds =
        new() { 7, 12 };

    private IReadOnlyList<Models.PartyMemberView> _latestPartyMembers = Array.Empty<Models.PartyMemberView>();

    public void ApplyPartyRestrictions(IReadOnlyList<Models.PartyMemberView> members)
    {
        _latestPartyMembers = members;
        ApplyRestrictionsToModes();
    }

    private void ApplyRestrictionsToModes()
    {
        foreach (var mode in MatchmakingModes)
        {
            string? restriction = null;
            foreach (var member in _latestPartyMembers)
            {
                if (!CanMemberPlayMode(member, mode.ModeId))
                {
                    restriction = FormatMemberRestriction(member);
                    break;
                }
            }
            mode.RestrictionText = restriction;
        }
    }

    private static bool CanMemberPlayMode(Models.PartyMemberView member, int modeId)
    {
        if (member.IsBanned)
        {
            // Permaban (> 2 years from now) blocks every mode
            if (IsPermaban(member.BannedUntil))
                return false;
            // Regular ban blocks human 5×5 modes only
            return !HumanGameModeIds.Contains(modeId);
        }

        // Not banned: honour the access map
        if (HumanGameModeIds.Contains(modeId)) return member.CanPlayHumanGames;
        if (SimpleModeIds.Contains(modeId)) return member.CanPlaySimpleModes;
        if (EducationModeIds.Contains(modeId)) return member.CanPlayEducation;
        return true;
    }

    private static bool TryGetBanExpiry(string? bannedUntil, out DateTimeOffset until)
    {
        until = default;
        return !string.IsNullOrEmpty(bannedUntil) &&
               DateTimeOffset.TryParse(bannedUntil, System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.AssumeUniversal, out until);
    }

    private static bool IsPermaban(string? bannedUntil)
        => !TryGetBanExpiry(bannedUntil, out var until) || until > DateTimeOffset.UtcNow.AddYears(2);

    private static string FormatMemberRestriction(Models.PartyMemberView member)
    {
        if (member.IsBanned)
        {
            if (!TryGetBanExpiry(member.BannedUntil, out var until) || until > DateTimeOffset.UtcNow.AddYears(2))
                return "Аккаунт заблокирован навсегда";
            return $"Поиск запрещён до {until.LocalDateTime:dd.MM.yyyy, HH:mm}";
        }
        return "Нет доступа к режиму";
    }

    private static string FormatModeCount(int n)
    {
        if (n == 1) return "1 РЕЖИМ";
        if (n >= 2 && n <= 4) return $"{n} РЕЖИМА";
        return $"{n} РЕЖИМОВ";
    }

    public void Dispose() => _queueTimer.Stop();
}
