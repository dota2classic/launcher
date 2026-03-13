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
    private static readonly IBrush BrushSearching     = new SolidColorBrush(Color.Parse("#1B5E2A"));
    private static readonly IBrush BrushIdle          = new SolidColorBrush(Color.Parse("#1B5E2A"));
    private static readonly IBrush BrushReadyHover    = new SolidColorBrush(Color.Parse("#2E86C1"));
    private static readonly IBrush BrushSearchingHover = new SolidColorBrush(Color.Parse("#246B32"));
    private static readonly IBrush BrushIdleHover     = new SolidColorBrush(Color.Parse("#246B32"));
    private static readonly IBrush BrushBorderReady     = new SolidColorBrush(Color.Parse("#2874A6"));
    private static readonly IBrush BrushBorderSearching = new SolidColorBrush(Color.Parse("#2E7A38"));
    private static readonly IBrush BrushBorderIdle      = new SolidColorBrush(Color.Parse("#2E7A38"));
    private static readonly IReadOnlyList<int> DefaultSelectedModeIds = new[] { 7 };

    private readonly IQueueSocketService _queueSocketService;
    private readonly IBackendApiService _backendApiService;
    private readonly ISettingsStorage _settingsStorage;
    private readonly DispatcherTimer _queueTimer;
    private DateTimeOffset? _enterQueueAt;
    private int _queuedModeCount;
    private MatchmakingMode[]? _queuedModes;
    private string[] _queuedModeNames = Array.Empty<string>();
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

    [ObservableProperty]
    private string _onlineStatsText = "";

    /// <summary>Blue when game ready, green when searching, dark gray when idle.</summary>
    public IBrush QueueButtonBackground => _hasServerUrl ? BrushReady
        : IsSearching ? BrushSearching : BrushIdle;

    /// <summary>Lighter version for hover state.</summary>
    public IBrush QueueButtonHoverBackground => _hasServerUrl ? BrushReadyHover
        : IsSearching ? BrushSearchingHover : BrushIdleHover;

    /// <summary>Subtle lighter border to give the button a framed look.</summary>
    public IBrush QueueButtonBorderBrush => _hasServerUrl ? BrushBorderReady
        : IsSearching ? BrushBorderSearching : BrushBorderIdle;

    /// <summary>Called when the user presses queue with no modes selected. Set by the parent VM.</summary>
    public Action? ShowNoModesSelectedToast { get; set; }

    public QueueViewModel(IQueueSocketService queueSocketService, IBackendApiService backendApiService, ISettingsStorage settingsStorage)
    {
        _queueSocketService = queueSocketService;
        _backendApiService = backendApiService;
        _settingsStorage = settingsStorage;

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
            var cancelModesStr = _queuedModes != null
                ? string.Join(",", _queuedModes.Select(m => ((int)m).ToString()))
                : "";
            var durationSec = _enterQueueAt.HasValue
                ? (long)DateTimeOffset.UtcNow.Subtract(_enterQueueAt.Value.UtcDateTime).TotalSeconds
                : 0L;
            d2c_launcher.Services.FaroTelemetryService.TrackEvent("cancel_queue",
                new Dictionary<string, string>
                {
                    ["modes"] = cancelModesStr,
                    ["duration_sec"] = durationSec.ToString(),
                });
            await _queueSocketService.LeaveAllQueuesAsync();
            return;
        }

        var selected = MatchmakingModes.Where(m => m.IsSelected)
            .Select(m => (MatchmakingMode)m.ModeId)
            .ToArray();
        if (selected.Length == 0)
        {
            ShowNoModesSelectedToast?.Invoke();
            return;
        }

        await _queueSocketService.EnterQueueAsync(selected);
        var modesStr = string.Join(",", selected.Select(m => ((int)m).ToString()));
        d2c_launcher.Services.FaroTelemetryService.TrackEvent("queue_entered",
            new System.Collections.Generic.Dictionary<string, string> { ["modes"] = modesStr });
    }

    public async Task RefreshMatchmakingModesAsync()
    {
        try
        {
            var modes = await _backendApiService.GetEnabledMatchmakingModesAsync();
            var settings = _settingsStorage.Get();
            var savedIds = settings.SelectedModeIds ?? (IReadOnlyList<int>)DefaultSelectedModeIds;

            var next = new ObservableCollection<MatchmakingModeView>();

            foreach (var mode in modes)
            {
                var existing = MatchmakingModes.FirstOrDefault(m => m.ModeId == mode.ModeId);
                // Prefer in-memory state if already loaded; otherwise use persisted selection.
                bool isSelected = existing?.IsSelected ?? savedIds.Contains(mode.ModeId);
                var view = new MatchmakingModeView(mode.ModeId, mode.Name, isSelected)
                {
                    InQueue = existing?.InQueue ?? 0
                };
                view.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(MatchmakingModeView.IsSelected))
                        PersistSelectedModes();
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

    private void PersistSelectedModes()
    {
        try
        {
            var settings = _settingsStorage.Get();
            var ids = MatchmakingModes.Where(m => m.IsSelected).Select(m => m.ModeId).ToList();
            settings.SelectedModeIds = ids.Count > 0 ? ids : null;
            _settingsStorage.Save(settings);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to persist selected matchmaking modes.", ex);
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
            _queuedModes = msg.Modes;
            _queuedModeCount = msg.Modes?.Length ?? 0;
            var names = MatchmakingModes
                .Where(m => msg.Modes!.Any(x => (int)x == m.ModeId))
                .Select(m => m.Name)
                .ToArray();
            _queuedModeNames = names;
            SearchingModesText = names.Length > 0
                ? $"В поиске: {string.Join(", ", names)}"
                : "В поиске";
        }
        else
        {
            _queuedModes = null;
            _queuedModeCount = 0;
            _queuedModeNames = Array.Empty<string>();
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
            QueueButtonModeCountText = _queuedModeNames.Length == 1
                ? _queuedModeNames[0].ToUpperInvariant()
                : (_queuedModeCount > 0 ? FormatModeCount(_queuedModeCount) : "");
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
        OnPropertyChanged(nameof(QueueButtonBorderBrush));
    }

    public void SetEnterQueueAt(DateTimeOffset? time) => _enterQueueAt = time;

    public void SetQueuedModeNames(string[] names)
    {
        _queuedModeNames = names;
        _queuedModeCount = names.Length;
    }

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

    private const int HighroomMmrRequired = 2500;

    private void ApplyRestrictionsToModes()
    {
        foreach (var mode in MatchmakingModes)
        {
            string? restriction = null;
            foreach (var member in _latestPartyMembers)
            {
                restriction = GetMemberModeRestriction(member, mode.ModeId);
                if (restriction != null)
                    break;
            }
            mode.RestrictionText = restriction;
        }
    }

    private static string? GetMemberModeRestriction(Models.PartyMemberView member, int modeId)
    {
        if (member.IsBanned)
        {
            if (IsPermaban(member.BannedUntil))
                return "Аккаунт заблокирован навсегда";
            // Regular ban blocks human 5×5 modes only
            if (HumanGameModeIds.Contains(modeId))
            {
                TryGetBanExpiry(member.BannedUntil, out var until);
                return $"Поиск запрещён до {until.LocalDateTime:dd.MM.yyyy HH:mm}";
            }
            return null;
        }

        if (HumanGameModeIds.Contains(modeId) && !member.CanPlayHumanGames)
            return "Для доступа выиграйте хотя бы одну игру";
        if (SimpleModeIds.Contains(modeId) && !member.CanPlaySimpleModes)
            return "Сыграйте против ботов для открытия режима";
        if (EducationModeIds.Contains(modeId) && !member.CanPlayEducation)
            return "Нет доступа к режиму";

        // Highroom requires minimum MMR across the party
        if (modeId == 8 && member.Mmr.HasValue && member.Mmr.Value < HighroomMmrRequired)
            return $"Нужно {HighroomMmrRequired} MMR (у {member.Name}: {member.Mmr.Value})";

        return null;
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

    private static string FormatModeCount(int n)
    {
        if (n == 1) return "1 РЕЖИМ";
        if (n >= 2 && n <= 4) return $"{n} РЕЖИМА";
        return $"{n} РЕЖИМОВ";
    }

    public void Dispose() => _queueTimer.Stop();
}
