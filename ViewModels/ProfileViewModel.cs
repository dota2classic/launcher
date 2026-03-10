using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class HeroRowViewModel : ViewModelBase
{
    public string HeroName { get; }
    public int Games { get; }
    public string WinRateText { get; }
    public string KdaText { get; }

    public HeroRowViewModel(HeroProfileData data)
    {
        HeroName = data.HeroName;
        Games = data.Games;
        WinRateText = $"{data.WinRate:0.00}%";
        KdaText = $"{data.Kda:0.00}";
    }
}

public partial class ProfileViewModel : ViewModelBase
{
    private readonly IBackendApiService _api;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _playerName = "—";
    [ObservableProperty] private string _winsLossesText = "—";
    [ObservableProperty] private string _winRateText = "—";
    [ObservableProperty] private int _mmr;
    [ObservableProperty] private int _rank;
    [ObservableProperty] private string _avgKills = "—";
    [ObservableProperty] private string _avgDeaths = "—";
    [ObservableProperty] private string _avgAssists = "—";

    public ObservableCollection<HeroRowViewModel> TopHeroes { get; } = new();

    public ProfileViewModel(IBackendApiService api)
    {
        _api = api;
    }

    public async Task LoadAsync(ulong steamId)
    {
        IsLoading = true;
        TopHeroes.Clear();
        try
        {
            var steamIdStr = steamId.ToString();
            var summaryTask = _api.GetPlayerSummaryAsync(steamIdStr);
            var heroesTask = _api.GetHeroStatsAsync(steamIdStr);
            await Task.WhenAll(summaryTask, heroesTask);

            var summary = summaryTask.Result;
            if (summary != null)
            {
                PlayerName = summary.Name;
                int total = summary.Wins + summary.Losses + summary.Abandons;
                WinsLossesText = $"{summary.Wins}–{summary.Losses}–{summary.Abandons}";
                WinRateText = total > 0 ? $"{(double)summary.Wins / total * 100:0.00}%" : "—";
                Mmr = summary.Mmr;
                Rank = summary.Rank;
                AvgKills = $"{summary.AvgKills:0.00}";
                AvgDeaths = $"{summary.AvgDeaths:0.00}";
                AvgAssists = $"{summary.AvgAssists:0.00}";
            }

            foreach (var h in heroesTask.Result)
                TopHeroes.Add(new HeroRowViewModel(h));
        }
        catch (Exception ex)
        {
            AppLog.Error($"ProfileViewModel.LoadAsync failed: {ex.Message}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
