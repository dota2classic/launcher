using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Api;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class StreamsViewModel : ObservableObject, IDisposable
{
    private static readonly PollSchedule StreamsSchedule = new(TimeSpan.FromMinutes(5));

    private readonly IBackendApiService _backendApiService;
    private readonly VisibilityAwarePoller _poller;

    [ObservableProperty] private bool _isLoading = true;
    [NotifyPropertyChangedFor(nameof(HasNoStreams))]
    [ObservableProperty] private bool _hasAnyStreams;

    public bool HasNoStreams => !HasAnyStreams;
    [ObservableProperty] private string? _playerSettingsUrl;

    public ObservableCollection<TwitchStreamDto> Streams { get; } = [];

    public StreamsViewModel(IBackendApiService backendApiService, ITimerFactory timerFactory, IWindowService windowService)
    {
        _backendApiService = backendApiService;
        _poller = new VisibilityAwarePoller(timerFactory, windowService, "StreamsViewModel.RefreshAsync",
            StreamsSchedule, RefreshAsync);
        _poller.Start(pollIfVisible: true);
    }

    public void RequestRefresh() => _poller.PollNow();

    private async Task RefreshAsync()
    {
        try
        {
            var streams = await _backendApiService.GetTwitchStreamsAsync();
            Streams.Clear();
            foreach (var s in streams)
                Streams.Add(s);
            HasAnyStreams = Streams.Count > 0;
        }
        catch (Exception ex)
        {
            AppLog.Error("StreamsViewModel.RefreshAsync", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Dispose() => _poller.Dispose();
}
