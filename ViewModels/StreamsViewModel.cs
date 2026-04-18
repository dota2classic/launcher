using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Api;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class StreamsViewModel : ObservableObject, IDisposable
{
    private readonly IBackendApiService _backendApiService;
    private readonly DispatcherTimer _pollTimer;
    private bool _isRefreshing;

    [ObservableProperty] private bool _isLoading = true;
    [NotifyPropertyChangedFor(nameof(HasNoStreams))]
    [ObservableProperty] private bool _hasAnyStreams;

    public bool HasNoStreams => !HasAnyStreams;
    [ObservableProperty] private string? _playerSettingsUrl;

    public ObservableCollection<TwitchStreamDto> Streams { get; } = [];

    public StreamsViewModel(IBackendApiService backendApiService)
    {
        _backendApiService = backendApiService;
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _pollTimer.Tick += (_, _) =>
        {
            if (!_isRefreshing)
                RefreshAsync().FireAndForget("StreamsViewModel.RefreshAsync");
        };
        InitAsync().FireAndForget("StreamsViewModel.InitAsync");
    }

    private async Task InitAsync()
    {
        await RefreshAsync();
        _pollTimer.Start();
    }

    public void RequestRefresh()
    {
        if (!_isRefreshing)
            RefreshAsync().FireAndForget("StreamsViewModel.RefreshAsync (requested)");
    }

    private async Task RefreshAsync()
    {
        _isRefreshing = true;
        try
        {
            var streams = await _backendApiService.GetTwitchStreamsAsync().ConfigureAwait(false);
            Dispatcher.UIThread.Post(() =>
            {
                Streams.Clear();
                foreach (var s in streams)
                    Streams.Add(s);
                HasAnyStreams = Streams.Count > 0;
                IsLoading = false;
                _isRefreshing = false;
            });
        }
        catch (Exception ex)
        {
            AppLog.Error("StreamsViewModel.RefreshAsync", ex);
            Dispatcher.UIThread.Post(() => { IsLoading = false; _isRefreshing = false; });
        }
    }

    public void Dispose() => _pollTimer.Stop();
}
