using System;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Api;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class RewardModalViewModel : ViewModelBase, IDisposable
{
    private static readonly Uri SubscriptionSplashUri = new("avares://d2c-launcher/Assets/Images/present.png");

    public Action? OnSubscriptionClaimed { get; set; }

    private readonly IBackendApiService _backendApiService;
    private string? _pendingNotificationId;
    private NotificationType? _pendingNotificationType;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _content = "";
    // Local bitmap (subscription splash loaded from avares)
    [ObservableProperty] private Bitmap? _localImage;
    // http(s):// URL (remote) or null — for AsyncImageLoader
    [ObservableProperty] private string? _remoteImageUrl;

    public bool HasLocalImage => LocalImage != null;
    public bool HasRemoteImage => RemoteImageUrl != null;
    public bool HasNoImage => LocalImage == null && RemoteImageUrl == null;

    public RewardModalViewModel(IBackendApiService backendApiService)
    {
        _backendApiService = backendApiService;
    }

    public void Dispose()
    {
        LocalImage?.Dispose();
        LocalImage = null;
    }

    public void Show(NotificationDto notification)
    {
        _pendingNotificationId = notification.Id;
        _pendingNotificationType = notification.NotificationType;
        Title = notification.Title;
        Content = notification.Content;

        var oldBitmap = LocalImage;
        LocalImage = null;
        RemoteImageUrl = null;

        switch (notification.NotificationType)
        {
            case NotificationType.SUBSCRIPTION_PURCHASED:
                using (var stream = AssetLoader.Open(SubscriptionSplashUri))
                    LocalImage = new Bitmap(stream);
                break;
            case NotificationType.ITEM_DROPPED:
                RemoteImageUrl = TryExtractImageUrl(notification.Params);
                break;
        }

        oldBitmap?.Dispose();
        OnPropertyChanged(nameof(HasLocalImage));
        OnPropertyChanged(nameof(HasRemoteImage));
        OnPropertyChanged(nameof(HasNoImage));
        IsOpen = true;
    }

    private static string? TryExtractImageUrl(object? raw)
    {
        if (raw is not JsonElement el) return null;
        if (el.TryGetProperty("image", out var img) && img.ValueKind == JsonValueKind.String)
            return img.GetString();
        return null;
    }

    [RelayCommand]
    private async Task ClaimAsync()
    {
        IsOpen = false;
        var id = _pendingNotificationId;
        var type = _pendingNotificationType;
        _pendingNotificationId = null;
        _pendingNotificationType = null;
        if (id == null) return;
        try
        {
            await _backendApiService.AcknowledgeNotificationAsync(id);
        }
        catch (Exception ex)
        {
            AppLog.Error("AcknowledgeNotification failed", ex);
        }
        if (type == NotificationType.SUBSCRIPTION_PURCHASED)
            OnSubscriptionClaimed?.Invoke();
    }
}
