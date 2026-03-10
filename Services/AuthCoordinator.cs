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
/// Subscribes to <see cref="SteamManager.OnSteamAuthorizationChanged"/>.
/// </summary>
public sealed class AuthCoordinator : IDisposable
{
    private readonly SteamManager _steamManager;
    private readonly IBackendApiService _backendApiService;
    private readonly IQueueSocketService _queueSocketService;
    private readonly ISettingsStorage _settingsStorage;

    private CancellationTokenSource? _cts;

    /// <summary>
    /// Raised on the UI thread after a token is successfully applied or cleared.
    /// Argument is null when cleared, non-null when applied and queue is connected.
    /// </summary>
    public event Action<string?>? TokenApplied;

    public string? CurrentToken { get; private set; }

    public AuthCoordinator(
        SteamManager steamManager,
        IBackendApiService backendApiService,
        IQueueSocketService queueSocketService,
        ISettingsStorage settingsStorage)
    {
        _steamManager = steamManager;
        _backendApiService = backendApiService;
        _queueSocketService = queueSocketService;
        _settingsStorage = settingsStorage;
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
            _ = EnsureQueueConnectionAsync(persistedToken, CancellationToken.None);
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

    private void PersistToken(string? token)
    {
        var settings = _settingsStorage.Get();
        settings.BackendAccessToken = token;
        _settingsStorage.Save(settings);
    }

    public void Dispose() => _cts?.Dispose();
}
