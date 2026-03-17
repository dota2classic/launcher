using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    public double WinRateBarWidth { get; }
    public double GamesBarWidth { get; }
    public double KdaBarWidth { get; }
    public string HeroImageUrl { get; }

    // color-coded by performance: green ≥60%, gold ≥50%, red <50%
    public IBrush WinRateBrush { get; }

    public HeroRowViewModel(HeroProfileData data, int maxGames, double maxWinRate, double maxKda)
    {
        HeroName = Models.HeroNames.GetLocalizedName(data.HeroName);
        Games = data.Games;
        var heroKey = Models.HeroNames.GetImageKey(data.HeroName);
        HeroImageUrl = $"avares://d2c-launcher/Assets/Images/Heroes/{heroKey}.webp";
        WinRateText = $"{data.WinRate:0.00}%";
        KdaText = $"{data.Kda:0.00}";
        GamesBarWidth = maxGames > 0 ? (double)data.Games / maxGames * 52 : 0;
        WinRateBarWidth = maxWinRate > 0 ? data.WinRate / maxWinRate * 52 : 0;
        KdaBarWidth = maxKda > 0 ? data.Kda / maxKda * 52 : 0;
        WinRateBrush = data.WinRate >= 60
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#52B847"))
            : data.WinRate >= 50
                ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#D4A843"))
                : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#D04535"));
    }
}

public partial class ProfileViewModel : ViewModelBase
{
    private readonly IBackendApiService _api;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _playerName = "—";
    [ObservableProperty] private string _playerInitial = "?";
    [ObservableProperty] private string? _avatarUrl;
    [ObservableProperty] private string _winsLossesText = "—";
    [ObservableProperty] private int _wins;
    [ObservableProperty] private int _losses;
    [ObservableProperty] private int _abandons;
    [ObservableProperty] private string _winRateText = "—";
    [ObservableProperty] private int _mmr;
    [ObservableProperty] private int _rank;
    [ObservableProperty] private int _totalGames;
    [ObservableProperty] private string _avgKills = "—";
    [ObservableProperty] private string _avgDeaths = "—";
    [ObservableProperty] private string _avgAssists = "—";
    [ObservableProperty] private string _abandonRateText = "—";
    [ObservableProperty] private string _playtimeText = "—";

    public ObservableCollection<HeroRowViewModel> TopHeroes { get; } = new();

    [ObservableProperty] private IReadOnlyList<Models.AspectData>? _aspects;

    [ObservableProperty] private bool _canGoBack;

    public Action? GoBackAction { get; set; }

    public ProfileViewModel(IBackendApiService api)
    {
        _api = api;
    }

    [RelayCommand]
    private void GoBack() => GoBackAction?.Invoke();

    private static string FormatPlaytime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}д, {ts.Hours}ч, {ts.Minutes}м";
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}ч, {ts.Minutes}м";
        return $"{ts.Minutes}м";
    }

    public async Task LoadAsync(string steamId)
    {
        IsLoading = true;
        TopHeroes.Clear();
        try
        {
            var steamIdStr = steamId;
            var summaryTask = _api.GetPlayerSummaryAsync(steamIdStr);
            var heroesTask = _api.GetHeroStatsAsync(steamIdStr);
            await Task.WhenAll(summaryTask, heroesTask);

            var summary = summaryTask.Result;
            if (summary != null)
            {
                PlayerName = summary.Name;
                PlayerInitial = summary.Name.Length > 0 ? summary.Name[0].ToString().ToUpper() : "?";
                AvatarUrl = summary.AvatarUrl;
                int total = summary.TotalGames;
                TotalGames = total;
                WinsLossesText = $"{summary.Wins}–{summary.Losses}–{summary.Abandons}";
                Wins = summary.Wins;
                Losses = summary.Losses;
                Abandons = summary.Abandons;
                WinRateText = total > 0 ? $"{(double)summary.Wins / total * 100:0.00}%" : "—";
                Mmr = summary.Mmr;
                Rank = summary.Rank;
                AvgKills = $"{summary.AvgKills:0.00}";
                AvgDeaths = $"{summary.AvgDeaths:0.00}";
                AvgAssists = $"{summary.AvgAssists:0.00}";
                AbandonRateText = $"{summary.SeasonAbandonRate * 100:0.0}%";
                PlaytimeText = FormatPlaytime(summary.SeasonPlaytimeSeconds);
                Aspects = summary.Aspects;
            }

            var heroes = heroesTask.Result;
            int maxGames = heroes.Count > 0 ? heroes.Max(h => h.Games) : 1;
            double maxWinRate = heroes.Count > 0 ? heroes.Max(h => h.WinRate) : 1;
            double maxKda = heroes.Count > 0 ? heroes.Max(h => h.Kda) : 1;
            foreach (var h in heroes)
                TopHeroes.Add(new HeroRowViewModel(h, maxGames, maxWinRate, maxKda));
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
