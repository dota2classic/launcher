using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Integration;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class GameLaunchViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsStorage _settingsStorage;
    private readonly IGameLaunchSettingsStorage _launchSettingsStorage;
    private readonly DispatcherTimer _runStateTimer;

    /// <summary>Counts ticks while game is running; config.cfg is synced every N ticks.</summary>
    private int _cfgSyncTickCounter;
    private const int CfgSyncIntervalTicks = 5; // ~7.5 s at 1.5 s/tick

    /// <summary>
    /// True when host_writeconfig was sent and we're waiting one tick for the engine to flush.
    /// On the next tick we read config.cfg and clear this flag.
    /// </summary>
    private bool _cfgFlushPending;

    /// <summary>
    /// True while settings are being updated from config.cfg.
    /// Checked by MainLauncherViewModel to avoid pushing the same values back to the game.
    /// </summary>
    public bool IsSyncingFromGame { get; private set; }

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

    public string LaunchButtonText => RunState switch
    {
        _ when !IsGameDirectorySet => "Select game",
        GameRunState.OurGameRunning => "Running",
        GameRunState.OtherDotaRunning => "Another dota running",
        _ => "Play"
    };

    public bool IsLaunchEnabled => !IsGameDirectorySet || RunState == GameRunState.None;

    public string PlayButtonText => RunState is GameRunState.OurGameRunning or GameRunState.OtherDotaRunning
        ? "Стоп"
        : "Играть";

    public bool PlayButtonIsStop => RunState is GameRunState.OurGameRunning or GameRunState.OtherDotaRunning;

    public GameLaunchViewModel(ISettingsStorage settingsStorage, IGameLaunchSettingsStorage launchSettingsStorage, IQueueSocketService queueSocketService)
    {
        _settingsStorage = settingsStorage;
        _launchSettingsStorage = launchSettingsStorage;
        var settings = settingsStorage.Get();
        _gameDirectory = settings.GameDirectory;
        _runState = GameRunState.None;

        _runStateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _runStateTimer.Tick += (_, _) => RefreshRunState();
        _runStateTimer.Start();
        RefreshRunState();

        queueSocketService.PlayerGameStateUpdated += msg =>
            Dispatcher.UIThread.Post(() => UpdateServerUrl(msg));
    }

    private void UpdateServerUrl(PlayerGameStateMessage? msg)
    {
        ServerUrl = msg?.ServerUrl;
    }

    public void SetGameDirectory(string? path)
    {
        GameDirectory = path;
        var settings = _settingsStorage.Get();
        settings.GameDirectory = path;
        _settingsStorage.Save(settings);
        RefreshRunState();
    }

    public void LaunchGame()
    {
        if (string.IsNullOrEmpty(GameDirectory))
            return;
        try
        {
            var exePath = Path.Combine(GameDirectory, "dota.exe");
            if (!File.Exists(exePath))
            {
                AppLog.Info($"LaunchGame: dota.exe not found at {exePath}");
                return;
            }

            var launchSettings = _launchSettingsStorage.Get();
            var cliArgs = CfgGenerator.BuildCliArgs(launchSettings);
            var execArg = CfgGenerator.Generate(launchSettings, GameDirectory);
            var arguments = execArg != null
                ? $"{cliArgs} {execArg}".Trim()
                : cliArgs;

            AppLog.Info($"LaunchGame: starting {exePath} {arguments}");
            using var _ = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                WorkingDirectory = GameDirectory,
                UseShellExecute = false,
            });

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
        }
        RefreshRunState();
    }

    public void ConnectToGame() => _ = ConnectToGameAsync();

    private async Task ConnectToGameAsync()
    {
        var url = ServerUrl;
        if (string.IsNullOrEmpty(url))
            return;

        AppLog.Info($"ConnectToGame: serverUrl={url}");

        if (RunState == GameRunState.OtherDotaRunning)
        {
            AppLog.Info("ConnectToGame: killing foreign Dota processes");
            KillAllDotaProcesses();
            await Task.Delay(1500);
            RefreshRunState();
        }

        if (RunState != GameRunState.OurGameRunning)
        {
            if (string.IsNullOrWhiteSpace(GameDirectory))
            {
                AppLog.Info("ConnectToGame: no game directory set, cannot launch");
                return;
            }

            AppLog.Info("ConnectToGame: launching our Dota");
            LaunchGame();
        }

        AppLog.Info("ConnectToGame: waiting for DOTA 2 window...");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(1500);
            if (DotaConsoleConnector.IsWindowOpen())
                break;
        }

        if (!DotaConsoleConnector.IsWindowOpen())
        {
            AppLog.Info("ConnectToGame: timed out waiting for DOTA 2 window");
            return;
        }

        await Task.Delay(3000);
        AppLog.Info($"ConnectToGame: sending 'connect {url}'");
        DotaConsoleConnector.SendCommand($"connect {url}");
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

            // Config.cfg sync: periodic while running + final read on game exit.
            // Two-phase approach: tick N sends host_writeconfig to flush cvars to disk,
            // tick N+1 reads config.cfg (giving the engine one frame to write the file).
            if (newState == GameRunState.OurGameRunning)
            {
                if (_cfgFlushPending)
                {
                    // Phase 2: engine had time to write — now read config.cfg
                    _cfgFlushPending = false;
                    ReadSettingsFromGame();
                }
                else
                {
                    _cfgSyncTickCounter++;
                    if (_cfgSyncTickCounter >= CfgSyncIntervalTicks)
                    {
                        _cfgSyncTickCounter = 0;
                        // Phase 1: tell the engine to flush config.cfg
                        FlushGameConfig();
                    }
                }
            }
            else
            {
                if (previousState == GameRunState.OurGameRunning)
                {
                    // Game just exited — clean shutdown already wrote config.cfg, just read it
                    ReadSettingsFromGame();
                }
                _cfgSyncTickCounter = 0;
                _cfgFlushPending = false;
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

    /// <summary>
    /// Phase 1: send <c>host_writeconfig</c> to the running game so it flushes
    /// all FCVAR_ARCHIVE cvars to config.cfg. The actual read happens on the next
    /// timer tick (see <see cref="_cfgFlushPending"/>), giving the engine a full
    /// frame to write the file.
    /// </summary>
    private void FlushGameConfig()
    {
        if (DotaConsoleConnector.IsWindowOpen())
        {
            DotaConsoleConnector.SendCommand("host_writeconfig");
            _cfgFlushPending = true;
        }
    }

    /// <summary>
    /// Phase 2 (or final sync on exit): read config.cfg and apply any changed
    /// cvar values back to the launcher settings.
    /// </summary>
    private void ReadSettingsFromGame()
    {
        if (string.IsNullOrWhiteSpace(GameDirectory))
            return;

        var settings = _launchSettingsStorage.Get();
        var changed = DotaCfgReader.ApplyToSettings(settings, GameDirectory);
        if (!changed)
            return;

        AppLog.Info("SyncSettingsFromGame: config.cfg had new values, updating launcher settings");
        IsSyncingFromGame = true;
        try
        {
            _launchSettingsStorage.Save(settings);
        }
        finally
        {
            IsSyncingFromGame = false;
        }
    }

    public void Dispose() => _runStateTimer.Stop();
}
