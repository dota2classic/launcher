using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Integration;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Resources;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class GameLaunchViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsStorage _settingsStorage;
    private readonly IGameLaunchSettingsStorage _launchSettingsStorage;
    private readonly ICvarSettingsProvider _cvarProvider;
    private readonly IVideoSettingsProvider _videoProvider;
    private readonly IBackendApiService _backendApiService;
    private readonly IQueueSocketService _queueSocketService;
    private readonly INetConService _netConService;
    private readonly IGameWindowService _gameWindowService;
    private readonly IDotakeysProfileService _dotakeysProfileService;
    private readonly DispatcherTimer _runStateTimer;

    /// <summary>
    /// Returns the Steam32 account ID of the currently logged-in user, or null if unknown.
    /// Set by the parent ViewModel after construction.
    /// </summary>
    public Func<ulong?>? GetCurrentSteamId32 { get; set; }

    [ObservableProperty]
    private string? _gameDirectory;

    [ObservableProperty]
    private GameRunState _runState;

    [ObservableProperty]
    private string? _serverUrl;

    public bool HasServerUrl => !string.IsNullOrEmpty(ServerUrl);

    partial void OnServerUrlChanged(string? value) => OnPropertyChanged(nameof(HasServerUrl));

    partial void OnRunStateChanged(GameRunState value) => NotifyLaunchProps();

    partial void OnGameDirectoryChanged(string? value)
    {
        OnPropertyChanged(nameof(IsGameDirectorySet));
        NotifyLaunchProps();
    }

    public bool IsGameDirectorySet => !string.IsNullOrWhiteSpace(GameDirectory);

    /// <summary>Fired when dota.exe is not found at launch (e.g. deleted by antivirus).</summary>
    public Action? OnExeNotFound { get; set; }

    public string LaunchButtonText => RunState switch
    {
        _ when !IsGameDirectorySet => "Select game",
        GameRunState.OurGameRunning => "Running",
        GameRunState.OtherDotaRunning => "Another dota running",
        _ => "Play"
    };

    public bool IsLaunchEnabled => !IsGameDirectorySet || RunState == GameRunState.None;

    public string PlayButtonText => RunState is GameRunState.OurGameRunning or GameRunState.OtherDotaRunning
        ? Strings.StopLabel
        : Strings.Launch;

    public bool PlayButtonIsStop => RunState is GameRunState.OurGameRunning or GameRunState.OtherDotaRunning;

    public GameLaunchViewModel(
        ISettingsStorage settingsStorage,
        IGameLaunchSettingsStorage launchSettingsStorage,
        ICvarSettingsProvider cvarProvider,
        IVideoSettingsProvider videoProvider,
        IQueueSocketService queueSocketService,
        IBackendApiService backendApiService,
        INetConService netConService,
        IGameWindowService gameWindowService,
        IDotakeysProfileService dotakeysProfileService)
    {
        _settingsStorage = settingsStorage;
        _launchSettingsStorage = launchSettingsStorage;
        _cvarProvider = cvarProvider;
        _videoProvider = videoProvider;
        _backendApiService = backendApiService;
        _queueSocketService = queueSocketService;
        _netConService = netConService;
        _gameWindowService = gameWindowService;
        _dotakeysProfileService = dotakeysProfileService;
        var settings = settingsStorage.Get();
        _gameDirectory = settings.GameDirectory;
        _runState = GameRunState.None;

        _runStateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _runStateTimer.Tick += (_, _) => RefreshRunState();
        _runStateTimer.Start();
        RefreshRunState();

        queueSocketService.PlayerGameStateUpdated += OnPlayerGameStateUpdated;
    }

    private void OnPlayerGameStateUpdated(PlayerGameStateMessage? msg) =>
        Dispatcher.UIThread.Post(() => UpdateServerUrl(msg));

    // Strict ip:port pattern — rejects embedded newlines, semicolons, or other injection chars.
    private static readonly Regex s_serverAddressRegex =
        new(@"^\d{1,3}(\.\d{1,3}){3}:\d{1,5}$", RegexOptions.Compiled);

    private static bool IsValidServerAddress(string? url) =>
        url != null && s_serverAddressRegex.IsMatch(url);

    private readonly ServerUrlTracker _serverUrlTracker = new();
    private CancellationTokenSource? _connectCts;
    // Written from MonitorProcessAsync (thread pool), read from RefreshRunState (UI thread).
    // volatile ensures the UI-thread read always sees the most recently written value.
    // int.MinValue is the sentinel meaning "no exit code captured yet".
    private volatile int _lastProcessExitCode = int.MinValue;

    private void UpdateServerUrl(PlayerGameStateMessage? msg)
    {
        var serverUrl = msg?.ServerUrl;
        ServerUrl = serverUrl;

        if (_serverUrlTracker.ShouldConnect(serverUrl) && _settingsStorage.Get().AutoConnectToGame)
        {
            AppLog.Info("GameLaunchViewModel: auto-connecting to server");
            ConnectToGame();
        }
    }

    public void SetGameDirectory(string? path)
    {
        GameDirectory = path;
        var settings = _settingsStorage.Get();
        settings.GameDirectory = path;
        _settingsStorage.Save(settings);

        // Load settings from the new game directory's config files
        if (!string.IsNullOrWhiteSpace(path))
        {
            _cvarProvider.LoadFromConfigCfg(path);
            _videoProvider.LoadFromVideoTxt(path);
        }

        RefreshRunState();
    }

    private static string[] TailConsoleLog(string gameDirectory, int lines = 40)
    {
        var logPath = Path.Combine(gameDirectory, "dota", "console.log");
        if (!File.Exists(logPath))
            return [];
        try
        {
            return File.ReadLines(logPath).TakeLast(lines).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private async Task ApplyWindowIconAsync(string exePath)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(1500);
            if (_gameWindowService.IsWindowOpen())
            {
                _gameWindowService.SetWindowIcon(exePath);
                return;
            }
        }
    }

    private async Task MonitorProcessAsync(Process process, string gameDirectory)
    {
        try
        {
            await process.WaitForExitAsync();
            var exitCode = process.ExitCode;
            _lastProcessExitCode = exitCode;
            // Exit code 0 = normal exit; -1 = killed by user (launcher stop button or Task Manager)
            if (exitCode != 0 && exitCode != -1)
            {
                AppLog.Error($"GameLaunchViewModel: dota.exe exited with code {exitCode}");
                var tail = TailConsoleLog(gameDirectory);
                if (tail.Length > 0)
                    AppLog.Warn($"GameLaunchViewModel: console.log tail:\n{string.Join("\n", tail)}");
                else
                    AppLog.Error("GameLaunchViewModel: console.log not found or empty");
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("GameLaunchViewModel: MonitorProcessAsync failed", ex);
        }
        finally
        {
            process.Dispose();
        }
    }

    public bool LaunchGame(string? launchOptions = null)
    {
        if (string.IsNullOrEmpty(GameDirectory))
            return false;
        try
        {
            var exePath = Path.Combine(GameDirectory, "dota.exe");
            if (!File.Exists(exePath))
            {
                AppLog.Info($"LaunchGame: dota.exe not found at {exePath}");
                OnExeNotFound?.Invoke();
                return false;
            }

            var launchSettings = _launchSettingsStorage.Get();
            var cliArgs = CfgGenerator.BuildCliArgs(launchSettings);
            var presetArg = CfgGenerator.WritePreset(GameDirectory, _cvarProvider.GetPresetCvars());
            var execArg = CfgGenerator.Generate(launchSettings, GameDirectory);

            cliArgs = string.IsNullOrEmpty(cliArgs) ? "-override_vpk" : $"{cliArgs} -override_vpk";

            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(cliArgs)) parts.Add(cliArgs);
            if (presetArg != null) parts.Add(presetArg);
            if (execArg != null) parts.Add(execArg);
            parts.Add(CfgGenerator.ImmutableServerCfgArg);
            if (!string.IsNullOrEmpty(launchOptions)) parts.Add(launchOptions);
            var arguments = string.Join(" ", parts);

            // Ensure the Dotaclassic keybind profile is migrated and patched before launch
            var steamId32 = GetCurrentSteamId32?.Invoke();
            if (steamId32.HasValue)
                _dotakeysProfileService.PrepareProfile(steamId32.Value);
            else
                AppLog.Warn("LaunchGame: Steam user unknown, skipping keybind profile patch.");

            AppLog.Info($"LaunchGame: starting {exePath} {arguments}");
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                WorkingDirectory = GameDirectory,
                UseShellExecute = true,
            });
            d2c_launcher.Services.FaroTelemetryService.TrackEvent("game_launched", new()
            {
                ["gpu_vendor"] = d2c_launcher.Services.FaroTelemetryService.GpuVendor,
                ["os_build"] = d2c_launcher.Services.FaroTelemetryService.OsBuild,
                ["vista_compat_enabled"] = d2c_launcher.Services.WindowsCompatibilityService
                    .IsVistaCompatEnabled(exePath).ToString().ToLowerInvariant(),
            });
            if (process != null)
                _ = MonitorProcessAsync(process, GameDirectory);
            _ = ApplyWindowIconAsync(exePath);

            // After first launch, enable -novid to skip the intro on subsequent launches
            if (!launchSettings.NoVid)
            {
                launchSettings.NoVid = true;
                _launchSettingsStorage.Save(launchSettings);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("LaunchGame failed.", ex);
            return false;
        }
        RefreshRunState();
        return true;
    }

    public void ConnectToGame()
    {
        _connectCts?.Cancel();
        _connectCts = new CancellationTokenSource();
        _ = ConnectToGameAsync(_connectCts.Token);
    }

    /// <summary>
    /// Returns true if the game is already connected to the server identified by <paramref name="serverUrl"/>.
    /// Sends <c>status</c> via NetCon and matches the port from the <c>type(dedicated)</c> line.
    /// Returns false on timeout (silent = not in a game) or port mismatch (different server/bots/end screen).
    /// </summary>
    private async Task<bool> IsAlreadyConnectedToAsync(string serverUrl, CancellationToken ct)
    {
        if (!_netConService.IsConnected)
        {
            AppLog.Warn("IsAlreadyConnectedTo: NetCon not connected, skipping status check");
            return false;
        }

        var targetPort = NetConStatusParser.ExtractTargetPort(serverUrl);
        if (targetPort == null) return false;

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnLine(string line)
        {
            if (line.Contains("type(dedicated)"))
                tcs.TrySetResult(line);
        }

        _netConService.LineReceived += OnLine;
        try
        {
            await _netConService.SendCommandAsync("status").ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(400));
            try
            {
                var udpLine = await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                var connected = NetConStatusParser.ParseServerPort(udpLine) == targetPort;
                AppLog.Info($"IsAlreadyConnectedTo: udpLine='{udpLine}' targetPort={targetPort} connected={connected}");
                return connected;
            }
            catch (OperationCanceledException)
            {
                ct.ThrowIfCancellationRequested(); // propagate outer cancel; swallow 400ms timeout
                AppLog.Info($"IsAlreadyConnectedTo: no response within 400ms for port {targetPort}, assuming not connected");
                return false;
            }
        }
        finally
        {
            _netConService.LineReceived -= OnLine;
        }
    }

    private async Task ConnectToGameAsync(CancellationToken ct)
    {
        var url = ServerUrl;
        if (string.IsNullOrEmpty(url))
            return;

        if (!IsValidServerAddress(url))
        {
            AppLog.Warn($"ConnectToGame: rejecting invalid server address '{url}'");
            return;
        }

        d2c_launcher.Services.FaroTelemetryService.TrackEvent("connect_pressed");
        AppLog.Info($"ConnectToGame: serverUrl={url}");

        if (RunState == GameRunState.OtherDotaRunning)
        {
            AppLog.Info("ConnectToGame: killing foreign Dota processes");
            KillAllDotaProcesses();
            await Task.Delay(1500, ct);
            RefreshRunState();
        }

        if (RunState != GameRunState.OurGameRunning)
        {
            if (string.IsNullOrWhiteSpace(GameDirectory))
            {
                AppLog.Info("ConnectToGame: no game directory set, cannot launch");
                return;
            }

            AppLog.Info($"ConnectToGame: launching our Dota with +connect {url}");
            LaunchGame($"+connect {url}");
            return;
        }

        // Game already running — wait for NetCon then send the connect command
        AppLog.Info("ConnectToGame: waiting for NetCon connection...");
        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadlineCts.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            await _netConService.WaitConnectedAsync(deadlineCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            AppLog.Info("ConnectToGame: timed out waiting for NetCon connection");
            if (!string.IsNullOrWhiteSpace(GameDirectory))
            {
                var tail = TailConsoleLog(GameDirectory);
                if (tail.Length > 0)
                    AppLog.Error($"ConnectToGame: console.log tail:\n{string.Join("\n", tail)}");
                else
                    AppLog.Error("ConnectToGame: console.log not found or empty");
            }
            return;
        }
        catch (OperationCanceledException)
        {
            AppLog.Info("ConnectToGame: cancelled (new connect attempt superseded this one)");
            return;
        }

        if (await IsAlreadyConnectedToAsync(url, ct).ConfigureAwait(false))
        {
            AppLog.Info($"ConnectToGame: already connected to {url}, skipping");
            return;
        }

        AppLog.Info($"ConnectToGame: sending 'connect {url}'");
        await _netConService.SendCommandAsync($"connect {url}").ConfigureAwait(false);
        _gameWindowService.FocusWindow();
    }

    /// <summary>
    /// Queries the live match, computes the spectator address (game_port + 1),
    /// launches the game if needed, and connects as a spectator.
    /// </summary>
    public void SpectateMatch(int matchId) => _ = SpectateMatchAsync(matchId);

    private async Task SpectateMatchAsync(int matchId)
    {
        AppLog.Info($"SpectateMatch: fetching live match {matchId}");
        var match = await _backendApiService.GetLiveMatchAsync(matchId).ConfigureAwait(false);
        if (match == null)
        {
            AppLog.Info($"SpectateMatch: match {matchId} not found or has no server");
            return;
        }

        // server is "ip:port"; spectator port = game_port + 1
        var parts = match.Server.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var gamePort))
        {
            AppLog.Info($"SpectateMatch: cannot parse server address '{match.Server}'");
            return;
        }

        var spectatorAddress = $"{parts[0]}:{gamePort + 1}";
        AppLog.Info($"SpectateMatch: connecting to spectator address {spectatorAddress}");
        FaroTelemetryService.TrackEvent("spectate_match", new() { ["match_id"] = matchId.ToString() });

        await Dispatcher.UIThread.InvokeAsync(() => ServerUrl = spectatorAddress);
        _connectCts?.Cancel();
        _connectCts = new CancellationTokenSource();
        await ConnectToGameAsync(_connectCts.Token).ConfigureAwait(false);
    }

    public void RefreshRunState()
    {
        if (!IsGameDirectorySet)
        {
            if (RunState != GameRunState.None)
            {
                RunState = GameRunState.None;
                NotifyLaunchProps();
            }
            return;
        }

        var ourExePath = GetOurDotaExePath();
        if (string.IsNullOrEmpty(ourExePath))
        {
            if (RunState != GameRunState.None)
            {
                RunState = GameRunState.None;
                NotifyLaunchProps();
            }
            return;
        }

        var processes = Process.GetProcessesByName("dota");
        try
        {
            var ourRunning = false;
            var otherRunning = false;
            foreach (var p in processes)
            {
                try
                {
                    var path = ProcessPathHelper.TryGetExecutablePath(p);
                    if (string.IsNullOrEmpty(path))
                        otherRunning = true;
                    else if (string.Equals(path, ourExePath, StringComparison.OrdinalIgnoreCase))
                        ourRunning = true;
                    else
                        otherRunning = true;
                }
                catch
                {
                    otherRunning = true;
                }
            }

            var newState = ourRunning
                ? GameRunState.OurGameRunning
                : (otherRunning ? GameRunState.OtherDotaRunning : GameRunState.None);

            var previousState = RunState;
            if (RunState != newState)
            {
                RunState = newState;
                NotifyLaunchProps();
            }

            // Track game running state so CvarSettingsProvider knows whether to write config.cfg
            _cvarProvider.IsGameRunning = newState == GameRunState.OurGameRunning;

            // Connect NetCon when game starts; disconnect when game exits
            if (previousState != GameRunState.OurGameRunning && newState == GameRunState.OurGameRunning)
            {
                _ = _netConService.StartConnectAsync();
            }
            else if (previousState == GameRunState.OurGameRunning && newState != GameRunState.OurGameRunning)
            {
                _netConService.Disconnect();
                var exitAttrs = new System.Collections.Generic.Dictionary<string, string>();
                var exitCode = _lastProcessExitCode;
                _lastProcessExitCode = int.MinValue;
                if (exitCode != int.MinValue)
                    exitAttrs["exit_code"] = exitCode.ToString();
                d2c_launcher.Services.FaroTelemetryService.TrackEvent("game_exited", exitAttrs);
                if (!string.IsNullOrWhiteSpace(GameDirectory))
                {
                    _cvarProvider.LoadFromConfigCfg(GameDirectory);
                    _videoProvider.LoadFromVideoTxt(GameDirectory);
                }
            }
        }
        finally
        {
            foreach (var p in processes)
                p.Dispose();
        }
    }

    private static void KillAllDotaProcesses()
    {
        var processes = Process.GetProcessesByName("dota");
        try
        {
            foreach (var p in processes)
            {
                // TODO: Log kill failures — bare catch hides access-denied / race conditions.
                try { p.Kill(entireProcessTree: true); } catch { }
            }
        }
        finally
        {
            foreach (var p in processes)
                p.Dispose();
        }
    }

    private string? GetOurDotaExePath()
    {
        if (string.IsNullOrWhiteSpace(GameDirectory))
            return null;
        var exe = Path.Combine(GameDirectory, "dota.exe");
        try
        {
            return Path.GetFullPath(exe);
        }
        catch
        {
            return null;
        }
    }

    public void StopGame()
    {
        KillAllDotaProcesses();
        RefreshRunState();
    }

    private void NotifyLaunchProps()
    {
        OnPropertyChanged(nameof(LaunchButtonText));
        OnPropertyChanged(nameof(IsLaunchEnabled));
        OnPropertyChanged(nameof(PlayButtonText));
        OnPropertyChanged(nameof(PlayButtonIsStop));
    }

    public void Dispose()
    {
        _queueSocketService.PlayerGameStateUpdated -= OnPlayerGameStateUpdated;
        _runStateTimer.Stop();
        _connectCts?.Cancel();
        _connectCts?.Dispose();
        _netConService.Disconnect();
    }
}
