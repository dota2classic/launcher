using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class LiveViewModel : ObservableObject, IDisposable
{
    private static readonly PollSchedule ListSchedule = new(TimeSpan.FromSeconds(5));

    private readonly IBackendApiService _backendApiService;
    private readonly VisibilityAwarePoller _poller;
    private bool _isActive;

    /// <summary>Called when the user clicks "Spectate" on the selected match.</summary>
    public Action<long>? OnSpectate { get; set; }

    /// <summary>Called when the user clicks a non-bot player name. Receives steam32 ID.</summary>
    public Action<string>? OnOpenProfile { get; set; }

    private Dictionary<int, string> _modeNames = new();

    [ObservableProperty] private LiveMatchCardViewModel? _selectedMatch;
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _hasNoMatches;

    public ObservableCollection<LiveMatchCardViewModel> Matches { get; } = [];

    public LiveViewModel(IBackendApiService backendApiService, ITimerFactory timerFactory, IWindowService windowService)
    {
        _backendApiService = backendApiService;
        _poller = new VisibilityAwarePoller(timerFactory, windowService, "LiveViewModel.RefreshListAsync",
            ListSchedule, RefreshListAsync, () => _isActive);
        // Timer starts only after InitAsync so mode names are populated before first refresh
        InitAsync().FireAndForget("LiveViewModel.InitAsync");
    }

    public void SetActive(bool isActive)
    {
        _isActive = isActive;
        _poller.Evaluate();
    }

    private async Task InitAsync()
    {
        try
        {
            var modes = await _backendApiService.GetAllMatchmakingModesAsync();
            _modeNames = modes.ToDictionary(m => m.ModeId, m => m.Name);
        }
        catch (Exception ex)
        {
            AppLog.Error("LiveViewModel.InitAsync (modes)", ex);
        }
        _poller.Start(pollIfVisible: true);
    }

    private string ResolveModeNamе(int modeId) =>
        _modeNames.TryGetValue(modeId, out var name) ? name : $"Режим {modeId}";

    private async Task RefreshListAsync()
    {
        try
        {
            var matches = await _backendApiService.GetLiveMatchesAsync();
            IsLoading = false;

            var liveIds = matches.Select(m => (long)m.MatchId).ToHashSet();
            for (int i = Matches.Count - 1; i >= 0; i--)
                if (!liveIds.Contains(Matches[i].MatchId))
                    Matches.RemoveAt(i);

            foreach (var dto in matches)
            {
                var id = (long)dto.MatchId;
                var card = Matches.FirstOrDefault(m => m.MatchId == id);
                if (card == null)
                {
                    card = new LiveMatchCardViewModel(id) { OnOpenProfile = OnOpenProfile };
                    Matches.Add(card);
                }
                card.UpdateFrom(dto, ResolveModeNamе((int)dto.MatchmakingMode));
            }

            HasNoMatches = Matches.Count == 0;

            if (SelectedMatch == null && Matches.Count > 0)
                SelectedMatch = Matches[0];
            else if (SelectedMatch != null && !Matches.Contains(SelectedMatch))
                SelectedMatch = Matches.Count > 0 ? Matches[0] : null;
        }
        catch (Exception ex)
        {
            AppLog.Error("LiveViewModel.RefreshListAsync", ex);
            IsLoading = false;
        }
    }


    [RelayCommand]
    private void SpectateSelectedMatch()
    {
        if (SelectedMatch == null) return;
        OnSpectate?.Invoke(SelectedMatch.MatchId);
    }

    public void Dispose()
    {
        _poller.Dispose();
    }
}
