using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Api;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class RewardModalViewModel : ViewModelBase
{
    private readonly IBackendApiService _backendApiService;
    private string? _pendingNotificationId;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _content = "";

    public RewardModalViewModel(IBackendApiService backendApiService)
    {
        _backendApiService = backendApiService;
    }

    public void Show(NotificationDto notification)
    {
        _pendingNotificationId = notification.Id;
        Title = notification.Title;
        Content = notification.Content;
        IsOpen = true;
    }

    [RelayCommand]
    private async Task ClaimAsync()
    {
        IsOpen = false;
        var id = _pendingNotificationId;
        _pendingNotificationId = null;
        if (id == null) return;
        try
        {
            await _backendApiService.AcknowledgeNotificationAsync(id);
        }
        catch (Exception ex)
        {
            AppLog.Error("AcknowledgeNotification failed", ex);
        }
    }
}
