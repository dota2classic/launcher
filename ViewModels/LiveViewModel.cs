using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class LiveViewModel : ObservableObject, IDisposable
{
    private readonly IBackendApiService _backendApiService;
    private readonly DispatcherTimer _pollTimer;

    private Dictionary<int, string> _modeNames = new();

    [ObservableProperty] private LiveMatchCardViewModel? _selectedMatch;
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _hasNoMatches;

    public ObservableCollection<LiveMatchCardViewModel> Matches { get; } = [];

    public LiveViewModel(IBackendApiService backendApiService)
    {
        _backendApiService = backendApiService;
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _pollTimer.Tick += (_, _) => FireAndForget(RefreshListAsync(), "LiveViewModel.RefreshListAsync");
        _pollTimer.Start();
        FireAndForget(InitAsync(), "LiveViewModel.InitAsync");
    }


    private async Task InitAsync()
    {
        try
        {
            var modes = await _backendApiService.GetEnabledMatchmakingModesAsync();
            _modeNames = modes.ToDictionary(m => m.ModeId, m => m.Name);
        }
        catch (Exception ex)
        {
            AppLog.Error("LiveViewModel.InitAsync (modes)", ex);
        }
        await RefreshListAsync();
    }

    private string ResolveModeNane(int modeId) =>
        _modeNames.TryGetValue(modeId, out var name) ? name : $"Режим {modeId}";

    private async Task RefreshListAsync()
    {
        try
        {
            var matches = await _backendApiService.GetLiveMatchesAsync();
            IsLoading = false;

            var liveIds = matches.Select(m => (int)m.MatchId).ToHashSet();
            for (int i = Matches.Count - 1; i >= 0; i--)
                if (!liveIds.Contains(Matches[i].MatchId))
                    Matches.RemoveAt(i);

            foreach (var dto in matches)
            {
                var id = (int)dto.MatchId;
                var card = Matches.FirstOrDefault(m => m.MatchId == id);
                if (card == null)
                {
                    card = new LiveMatchCardViewModel(id);
                    Matches.Add(card);
                }
                card.UpdateFrom(dto, ResolveModeNane((int)dto.MatchmakingMode));
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


    private static async void FireAndForget(Task task, string context)
    {
        try { await task; }
        catch (Exception ex) { AppLog.Error($"FireAndForget({context})", ex); }
    }

    public void Dispose()
    {
        _pollTimer.Stop();
    }
}
