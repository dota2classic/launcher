using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Services;
using d2c_launcher.Util;
using d2c_launcher.Api;

namespace d2c_launcher.ViewModels;

public partial class StoreViewModel : ViewModelBase
{
    private readonly IBackendApiService _api;

    public Func<ulong?>? GetCurrentSteamId { get; set; }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasPlusSubscription;
    [ObservableProperty] private string _plusSubscriptionEndText = "—";

    public string StoreButtonText => HasPlusSubscription
        ? I18n.T("store.subscriptionRenew")
        : I18n.T("store.subscriptionBuy");

    public StoreViewModel(IBackendApiService api)
    {
        _api = api;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var me = await _api.GetMeAsync();
            var oldRole = me?.User?.Roles?.FirstOrDefault(r => r.Role == Role.OLD);
            HasPlusSubscription = oldRole != null;
            if (oldRole != null && DateTime.TryParse(oldRole.EndTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
                PlusSubscriptionEndText = end.ToString("d MMMM yyyy", new CultureInfo("ru-RU"));
            else
                PlusSubscriptionEndText = "—";
        }
        catch (Exception ex)
        {
            AppLog.Error($"StoreViewModel.LoadAsync failed: {ex.Message}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenTelegram()
    {
        var steamId = GetCurrentSteamId?.Invoke();
        var url = steamId.HasValue
            ? $"https://t.me/dotaclassic_payments_bot?start={steamId.Value}"
            : "https://t.me/dotaclassic_payments_bot";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    partial void OnHasPlusSubscriptionChanged(bool value) =>
        OnPropertyChanged(nameof(StoreButtonText));
}
