using System;
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
    private CancellationTokenSource? _detailCts;

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
        FireAndForget(RefreshListAsync(), "LiveViewModel.RefreshListAsync (startup)");
    }

    partial void OnSelectedMatchChanged(LiveMatchCardViewModel? value)
    {
        _detailCts?.Cancel();
        _detailCts?.Dispose();
        _detailCts = null;
        if (value == null) return;
        _detailCts = new CancellationTokenSource();
        FireAndForget(RunDetailLoopAsync(value.MatchId, _detailCts.Token), "LiveViewModel.RunDetailLoopAsync");
    }

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
                card.UpdateFrom(dto);
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

    private async Task RunDetailLoopAsync(int matchId, CancellationToken ct)
    {
        try
        {
            await foreach (var dto in _backendApiService.SubscribeLiveMatchAsync(matchId, ct))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (SelectedMatch?.MatchId == matchId)
                        SelectedMatch.UpdateFrom(dto);
                });
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppLog.Error($"LiveViewModel.RunDetailLoopAsync({matchId})", ex); }
    }

    private static async void FireAndForget(Task task, string context)
    {
        try { await task; }
        catch (Exception ex) { AppLog.Error($"FireAndForget({context})", ex); }
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _detailCts?.Cancel();
        _detailCts?.Dispose();
    }
}
