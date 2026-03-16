using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using d2c_launcher.Integration;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

/// <summary>
/// Coordinates backend JWT token application: sets it on the API client,
/// connects/disconnects the queue socket, and persists it to settings.
/// Subscribes to <see cref="ISteamManager.OnSteamAuthorizationChanged"/>.
/// Periodically refreshes the token so it never goes stale (TTL ~4-5 days).
/// </summary>
public sealed class AuthCoordinator : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);

    private readonly ISteamManager _steamManager;
    private readonly IBackendApiService _backendApiService;
    private readonly IQueueSocketService _queueSocketService;
    private readonly ISettingsStorage _settingsStorage;
    private readonly ISteamAuthApi _steamAuthApi;

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _refreshCts;

    /// <summary>
    /// Raised on the UI thread after a token is successfully applied or cleared.
    /// Argument is null when cleared, non-null when applied and queue is connected.
    /// </summary>
    public event Action<string?>? TokenApplied;

    public string? CurrentToken { get; private set; }

    public AuthCoordinator(
        ISteamManager steamManager,
        IBackendApiService backendApiService,
        IQueueSocketService queueSocketService,
        ISettingsStorage settingsStorage,
        ISteamAuthApi steamAuthApi)
    {
        _steamManager = steamManager;
        _backendApiService = backendApiService;
        _queueSocketService = queueSocketService;
        _settingsStorage = settingsStorage;
        _steamAuthApi = steamAuthApi;
    }

    /// <summary>
    /// Subscribes to Steam auth events and applies any already-available token.
    /// Call once after construction, passing the persisted token from settings.
    /// </summary>
    public void Start(string? persistedToken)
    {
        if (!string.IsNullOrWhiteSpace(persistedToken))
        {
            CurrentToken = persistedToken;
            _backendApiService.SetBearerToken(persistedToken);
        }

        _steamManager.OnSteamAuthorizationChanged += token =>
            Dispatcher.UIThread.Post(() => _ = ApplyTokenAsync(token));

        var currentTicket = _steamManager.CurrentAuthTicket;
        if (!string.IsNullOrWhiteSpace(currentTicket))
            _ = ApplyTokenAsync(currentTicket);
        else if (!string.IsNullOrWhiteSpace(persistedToken))
        {
            _ = EnsureQueueConnectionAsync(persistedToken, CancellationToken.None);
            StartRefreshLoop(persistedToken);
        }
    }

    private async Task ApplyTokenAsync(string? token)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        if (string.IsNullOrWhiteSpace(token))
        {
            AppLog.Info("Backend token cleared.");
            CurrentToken = null;
            _backendApiService.SetBearerToken(null);
            PersistToken(null);
            await EnsureQueueConnectionAsync(null, ct);
            TokenApplied?.Invoke(null);
            return;
        }

        try
        {
            AppLog.Info("Backend token received from bridge.");
            CurrentToken = token;
            _backendApiService.SetBearerToken(token);
            PersistToken(token);
            await EnsureQueueConnectionAsync(token, ct);
            StartRefreshLoop(token);
            TokenApplied?.Invoke(token);
        }
        catch (OperationCanceledException)
        {
            AppLog.Info("Backend token application canceled.");
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;

            AppLog.Error("Backend token application failed.", ex);
            CurrentToken = null;
            _backendApiService.SetBearerToken(null);
            PersistToken(null);
            await EnsureQueueConnectionAsync(null, ct);
            TokenApplied?.Invoke(null);
        }
    }

    private async Task EnsureQueueConnectionAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            await _queueSocketService.DisconnectAsync(ct);
        else
            await _queueSocketService.ConnectAsync(token, ct);
    }

    private void StartRefreshLoop(string token)
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();
        _ = RefreshLoopAsync(token, _refreshCts.Token);
    }

    private async Task RefreshLoopAsync(string initialToken, CancellationToken ct)
    {
        var token = initialToken;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(RefreshInterval, ct).ConfigureAwait(false);

                AppLog.Info("Attempting periodic token refresh...");
                var newToken = await _steamAuthApi.RefreshTokenAsync(token, ct).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(newToken))
                {
                    token = newToken;
                    CurrentToken = token;
                    _backendApiService.SetBearerToken(token);
                    PersistToken(token);
                    await _queueSocketService.ConnectAsync(token, ct).ConfigureAwait(false);
                    AppLog.Info("Token refreshed and applied.");
                }
                else
                {
                    // Refresh failed — try re-authenticating via SteamBridge if a ticket is available
                    AppLog.Info("Token refresh returned null; attempting Steam re-auth.");
                    var ticket = _steamManager.CurrentAuthTicket;
                    if (!string.IsNullOrWhiteSpace(ticket))
                        Dispatcher.UIThread.Post(() => _ = ApplyTokenAsync(ticket));
                    return; // ApplyTokenAsync will restart the loop
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            AppLog.Error("Token refresh loop failed unexpectedly.", ex);
        }
    }

    private void PersistToken(string? token)
    {
        var settings = _settingsStorage.Get();
        settings.BackendAccessToken = token;
        _settingsStorage.Save(settings);
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
    }
}
