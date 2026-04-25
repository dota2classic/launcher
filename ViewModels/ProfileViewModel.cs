using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
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

public partial class DodgeEntryViewModel : ViewModelBase
{
    private readonly IBackendApiService _api;
    private readonly Action<DodgeEntryViewModel> _removeFromList;

    public string SteamId { get; }
    public string Name { get; }
    public string? AvatarUrl { get; }
    public string AddedDateText { get; }

    [ObservableProperty] private bool _isRemoving;

    public DodgeEntryViewModel(Api.DodgeListEntryDto dto, IBackendApiService api, Action<DodgeEntryViewModel> removeFromList)
    {
        _api = api;
        _removeFromList = removeFromList;
        SteamId = dto.User.SteamId ?? "";
        Name = dto.User.Name ?? SteamId;
        AvatarUrl = dto.User.AvatarSmall ?? dto.User.Avatar;
        AddedDateText = DateTime.TryParse(dto.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("d MMM yyyy", new CultureInfo("ru-RU"))
            : dto.CreatedAt;
    }

    [RelayCommand]
    private async Task RemoveDodgeAsync()
    {
        IsRemoving = true;
        try
        {
            await _api.RemoveDodgeAsync(SteamId);
            _removeFromList(this);
        }
        catch (Exception ex)
        {
            AppLog.Error($"RemoveDodge failed for {SteamId}: {ex.Message}", ex);
        }
        finally
        {
            IsRemoving = false;
        }
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

    // ── Owner / subscription tab ─────────────────────────────────────────────
    [ObservableProperty] private bool _isOwner;
    [ObservableProperty] private bool _isGeneralTabActive = true;
    [ObservableProperty] private bool _isSubscriptionTabActive;
    [ObservableProperty] private bool _isSubscriptionDataLoading;
    [ObservableProperty] private bool _hasPlusSubscription;
    [ObservableProperty] private string _plusSubscriptionEndText = "—";
    private bool _subscriptionDataLoaded;

    public string StoreButtonText => HasPlusSubscription
        ? I18n.T("profile.subscriptionRenew")
        : I18n.T("profile.subscriptionBuy");

    partial void OnHasPlusSubscriptionChanged(bool value) => OnPropertyChanged(nameof(StoreButtonText));

    public ObservableCollection<DodgeEntryViewModel> DodgeList { get; } = new();

    // ── Dodge search modal ───────────────────────────────────────────────────
    [ObservableProperty] private bool _isDodgeSearchOpen;
    [ObservableProperty] private string _dodgeSearchText = "";
    public ObservableCollection<InviteCandidateView> DodgeCandidates { get; } = new();
    private CancellationTokenSource? _dodgeSearchCts;

    public Action? GoBackAction { get; set; }

    public ProfileViewModel(IBackendApiService api)
    {
        _api = api;
    }

    [RelayCommand]
    private void GoBack() => GoBackAction?.Invoke();

    [RelayCommand]
    private void SelectGeneralTab()
    {
        IsGeneralTabActive = true;
        IsSubscriptionTabActive = false;
    }

    [RelayCommand]
    private void SelectSubscriptionTab()
    {
        IsGeneralTabActive = false;
        IsSubscriptionTabActive = true;
        if (IsOwner && !_subscriptionDataLoaded && !IsSubscriptionDataLoading)
            LoadSubscriptionDataAsync().FireAndForget("LoadSubscriptionDataAsync");
    }

    [RelayCommand]
    private void OpenStore() =>
        Process.Start(new ProcessStartInfo("https://dotaclassic.ru/store") { UseShellExecute = true });

    [RelayCommand]
    private void OpenDodgeSearch()
    {
        DodgeCandidates.Clear();
        DodgeSearchText = "";
        IsDodgeSearchOpen = true;
        SearchDodgeCandidatesAsync("").FireAndForget("SearchDodgeCandidatesInitial");
    }

    [RelayCommand]
    private void CloseDodgeSearch()
    {
        IsDodgeSearchOpen = false;
        _dodgeSearchCts?.Cancel();
    }

    partial void OnDodgeSearchTextChanged(string value) =>
        SearchDodgeCandidatesAsync(value).FireAndForget("SearchDodgeCandidates");

    private async Task SearchDodgeCandidatesAsync(string query)
    {
        _dodgeSearchCts?.Cancel();
        _dodgeSearchCts = new CancellationTokenSource();
        var ct = _dodgeSearchCts.Token;
        try
        {
            await Task.Delay(200, ct);
            var results = await _api.SearchPlayersAsync(string.IsNullOrWhiteSpace(query) ? "a" : query, 25, ct);
            if (ct.IsCancellationRequested) return;
            DodgeCandidates.Clear();
            foreach (var r in results)
                DodgeCandidates.Add(r);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppLog.Error($"DodgeSearch failed: {ex.Message}", ex);
        }
    }

    public async Task DodgePlayerAsync(InviteCandidateView candidate)
    {
        try
        {
            await _api.DodgePlayerAsync(candidate.SteamId);
            _subscriptionDataLoaded = false;
            IsDodgeSearchOpen = false;
            if (IsSubscriptionTabActive)
                LoadSubscriptionDataAsync().FireAndForget("LoadSubscriptionDataAsync");
        }
        catch (Exception ex)
        {
            AppLog.Error($"DodgePlayer failed for {candidate.SteamId}: {ex.Message}", ex);
        }
    }

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
        IsGeneralTabActive = true;
        IsSubscriptionTabActive = false;
        _subscriptionDataLoaded = false;
        TopHeroes.Clear();
        DodgeList.Clear();
        HasPlusSubscription = false;
        PlusSubscriptionEndText = "—";
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

    private async Task LoadSubscriptionDataAsync()
    {
        IsSubscriptionDataLoading = true;
        DodgeList.Clear();
        try
        {
            var meTask = _api.GetMeAsync();
            var dodgeTask = _api.GetDodgeListAsync();
            await Task.WhenAll(meTask, dodgeTask);

            var me = meTask.Result;
            var oldRole = me?.User?.Roles?.FirstOrDefault(r => r.Role == Api.Role.OLD);
            HasPlusSubscription = oldRole != null;
            if (oldRole != null && DateTime.TryParse(oldRole.EndTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
                PlusSubscriptionEndText = end.ToString("d MMMM yyyy", new CultureInfo("ru-RU"));
            else
                PlusSubscriptionEndText = "—";

            foreach (var entry in dodgeTask.Result)
                DodgeList.Add(new DodgeEntryViewModel(entry, _api, e => DodgeList.Remove(e)));

            _subscriptionDataLoaded = true;
        }
        catch (Exception ex)
        {
            AppLog.Error($"LoadSubscriptionDataAsync failed: {ex.Message}", ex);
        }
        finally
        {
            IsSubscriptionDataLoading = false;
        }
    }
}
