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
    private readonly ICvarSettingsProvider _cvarProvider;
    private readonly IVideoSettingsProvider _videoProvider;
    private readonly DispatcherTimer _runStateTimer;

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

    public GameLaunchViewModel(
        ISettingsStorage settingsStorage,
        IGameLaunchSettingsStorage launchSettingsStorage,
        ICvarSettingsProvider cvarProvider,
        IVideoSettingsProvider videoProvider,
        IQueueSocketService queueSocketService)
    {
        _settingsStorage = settingsStorage;
        _launchSettingsStorage = launchSettingsStorage;
        _cvarProvider = cvarProvider;
        _videoProvider = videoProvider;
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

        // Load settings from the new game directory's config files
        if (!string.IsNullOrWhiteSpace(path))
        {
            _cvarProvider.LoadFromConfigCfg(path);
            _videoProvider.LoadFromVideoTxt(path);
        }

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
            d2c_launcher.Services.FaroTelemetryService.TrackEvent("game_launched");

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

            // Track game running state so CvarSettingsProvider knows whether to write config.cfg
            _cvarProvider.IsGameRunning = newState == GameRunState.OurGameRunning;

            // On game exit: re-read config files to capture any in-game changes
            if (previousState == GameRunState.OurGameRunning && newState != GameRunState.OurGameRunning)
            {
                d2c_launcher.Services.FaroTelemetryService.TrackEvent("game_exited");
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

    public void Dispose() => _runStateTimer.Stop();
}
