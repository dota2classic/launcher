using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class PartyViewModel : ViewModelBase
{
    private readonly IQueueSocketService _queueSocketService;
    private readonly IBackendApiService _backendApiService;
    private readonly DispatcherTimer _partyRefreshTimer;
    private CancellationTokenSource? _inviteSearchCts;
    private int _partyRefreshRunning;
    private System.Collections.Generic.HashSet<string> _onlineUsers = new(StringComparer.Ordinal);

    // Delegate set by parent
    public Func<string?> GetBackendToken { get; set; } = () => null;

    [ObservableProperty]
    private ObservableCollection<PartyMemberView> _partyMembers = new();

    [ObservableProperty]
    private bool _isInviteModalOpen;

    [ObservableProperty]
    private string _inviteSearchText = "";

    [ObservableProperty]
    private ObservableCollection<InviteCandidateView> _inviteCandidates = new();

    public bool CanInviteToParty => PartyMembers.Count < 5;
    public bool CanLeaveParty => PartyMembers.Count > 1;

    public IRelayCommand CloseInviteModalCommand { get; }
    public IRelayCommand LeavePartyCommand { get; }

    public PartyViewModel(IQueueSocketService queueSocketService, IBackendApiService backendApiService)
    {
        _queueSocketService = queueSocketService;
        _backendApiService = backendApiService;

        _partyRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _partyRefreshTimer.Tick += (_, _) => { _ = RefreshPartyAsync(); };
        _partyRefreshTimer.Start();

        queueSocketService.PartyUpdated += party => Dispatcher.UIThread.Post(() => { _ = RefreshPartyAsync(); });
        queueSocketService.OnlineUpdated += msg => Dispatcher.UIThread.Post(() => UpdateOnlineUsers(msg));

        CloseInviteModalCommand = new RelayCommand(CloseInviteModal);
        LeavePartyCommand = new AsyncRelayCommand(LeavePartyAsync);
    }

    public void OpenInviteModal()
    {
        IsInviteModalOpen = true;
        _ = LoadInitialInviteCandidatesAsync();
    }

    public void CloseInviteModal()
    {
        IsInviteModalOpen = false;
        InviteSearchText = "";
    }

    partial void OnInviteSearchTextChanged(string value)
    {
        AppLog.Info($"Invite search text changed: '{value}'");
        _ = SearchInviteCandidatesAsync(value);
    }

    public async Task RefreshPartyAsync()
    {
        if (Interlocked.Exchange(ref _partyRefreshRunning, 1) != 0)
            return;

        try
        {
            var token = GetBackendToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                AppLog.Info("Party refresh skipped: no backend token.");
                ClearParty();
                return;
            }

            var partySnapshot = await _backendApiService.GetMyPartySnapshotAsync(token);
            DisposePartyAvatars();
            PartyMembers.Clear();
            foreach (var m in partySnapshot.Members)
                PartyMembers.Add(m);

            // Notify parent to update queue timer and restrictions
            EnterQueueAtChanged?.Invoke(partySnapshot.EnterQueueAt);
            PartyMembersChanged?.Invoke(partySnapshot.Members);
            OnPropertyChanged(nameof(CanInviteToParty));
            OnPropertyChanged(nameof(CanLeaveParty));
        }
        catch (Exception ex)
        {
            AppLog.Error("Party refresh failed.", ex);
            ClearParty();
        }
        finally
        {
            Interlocked.Exchange(ref _partyRefreshRunning, 0);
        }
    }

    /// <summary>Raised when the queue start timestamp changes, so QueueViewModel can update its timer.</summary>
    public event Action<DateTimeOffset?>? EnterQueueAtChanged;

    /// <summary>Raised after every party refresh with the current member list (for restriction propagation).</summary>
    public event Action<IReadOnlyList<Models.PartyMemberView>>? PartyMembersChanged;

    public void ClearParty()
    {
        DisposePartyAvatars();
        PartyMembers.Clear();
        EnterQueueAtChanged?.Invoke(null);
        PartyMembersChanged?.Invoke(Array.Empty<Models.PartyMemberView>());
        OnPropertyChanged(nameof(CanInviteToParty));
        OnPropertyChanged(nameof(CanLeaveParty));
    }

    private async Task LeavePartyAsync()
    {
        try { await _queueSocketService.LeavePartyAsync(); }
        catch (Exception ex) { AppLog.Error("Failed to leave party.", ex); }
    }

    private void UpdateOnlineUsers(OnlineUpdateMessage msg)
    {
        _onlineUsers = msg.Online?.ToHashSet(StringComparer.Ordinal) ?? new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in InviteCandidates)
            candidate.IsOnline = _onlineUsers.Contains(candidate.SteamId);
    }

    public async Task InvitePlayerAsync(string steamId)
    {
        if (string.IsNullOrWhiteSpace(steamId))
            return;
        try
        {
            await _queueSocketService.InviteToPartyAsync(steamId);
            AppLog.Info($"Invite sent for steamId: {steamId}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to invite player to party.", ex);
        }
    }

    private async Task SearchInviteCandidatesAsync(string query)
    {
        _inviteSearchCts?.Cancel();
        _inviteSearchCts?.Dispose();
        _inviteSearchCts = new CancellationTokenSource();
        var ct = _inviteSearchCts.Token;

        try
        {
            AppLog.Info($"Invite search started: '{query}'");
            await Task.Delay(300, ct);
            if (ct.IsCancellationRequested)
                return;

            if (string.IsNullOrWhiteSpace(query) || query.Length < 1)
            {
                AppLog.Info("Invite search cleared (empty query).");
                InviteCandidates = new ObservableCollection<InviteCandidateView>();
                return;
            }

            var results = await _backendApiService.SearchPlayersAsync(query, 25, ct);
            if (ct.IsCancellationRequested)
                return;

            AppLog.Info($"Invite search results: {results.Count}");
            Dispatcher.UIThread.Post(() =>
            {
                InviteCandidates = new ObservableCollection<InviteCandidateView>(results);
                foreach (var candidate in InviteCandidates)
                    candidate.IsOnline = _onlineUsers.Contains(candidate.SteamId);
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppLog.Error("Failed to search players.", ex);
        }
    }

    private async Task LoadInitialInviteCandidatesAsync()
    {
        if (!string.IsNullOrWhiteSpace(InviteSearchText))
            return;
        try
        {
            AppLog.Info("Loading initial invite candidates.");
            var results = await _backendApiService.SearchPlayersAsync("a", 10);
            Dispatcher.UIThread.Post(() =>
            {
                InviteCandidates = new ObservableCollection<InviteCandidateView>(results);
            });
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to load initial invite candidates.", ex);
        }
    }

    private void DisposePartyAvatars()
    {
        foreach (var member in PartyMembers)
            member.AvatarImage?.Dispose();
    }
}
