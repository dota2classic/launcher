using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Integration;

[SupportedOSPlatform("windows")]
public class SteamManager : IDisposable
{
    private const string BridgeExeName = "d2c-steam-bridge.exe";
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _monitorTask;
    private ulong _lastActiveUser;
    private string? _steamAuthTicket;
    private int _bridgeFailStreak;

    public event Action<User?>? OnUserUpdated;
    public event Action<SteamStatus>? OnSteamStatusUpdated;
    public event Action<string?>? OnSteamAuthorizationChanged;

    public User? CurrentUser { get; private set; }
    public SteamStatus SteamStatus { get; private set; } = SteamStatus.NotRunning;
    public string? CurrentAuthTicket => _steamAuthTicket;

    public SteamManager()
    {
        _monitorTask = Task.Run(() => MonitorLoop(_shutdown.Token));
    }

    public void PollSteamState()
    {
        // Legacy no-op; state updates come from background monitor loop.
    }

    private async Task MonitorLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var steamRunning = IsSteamProcessRunning();
                var activeUser = steamRunning ? TryReadActiveUserSteamId() : 0UL;

                var nextStatus = !steamRunning
                    ? SteamStatus.NotRunning
                    : activeUser == 0
                        ? SteamStatus.Offline
                        : SteamStatus.Running;

                SetSteamStatus(nextStatus);

                if (nextStatus != SteamStatus.Running)
                {
                    _lastActiveUser = 0;
                    _bridgeFailStreak = 0;
                    SetUser(null);
                    SetAuthTicket(null);
                }
                else if (activeUser != 0 && activeUser != _lastActiveUser)
                {
                    AppLog.Info($"[SteamManager] New active Steam user {activeUser}, querying bridge...");
                    var snapshot = await QueryBridgeSnapshotAsync(ct);

                    if (snapshot?.SteamId.HasValue == true && !string.IsNullOrWhiteSpace(snapshot.PersonaName))
                    {
                        _bridgeFailStreak = 0;
                        var user = new User(
                            snapshot.SteamId.Value,
                            snapshot.PersonaName,
                            snapshot.AvatarRgba,
                            snapshot.AvatarWidth ?? 0,
                            snapshot.AvatarHeight ?? 0);
                        SetUser(user);

                        if (snapshot.AuthTicket != null)
                        {
                            // Full success: advance user marker so we stop re-querying.
                            _lastActiveUser = activeUser;
                            SetAuthTicket(snapshot.AuthTicket);
                            AppLog.Info($"[SteamManager] Auth ticket acquired for user {activeUser}.");
                        }
                        else
                        {
                            // User info OK but auth ticket timed out in bridge — show the user
                            // in the UI but retry the full bridge query next tick.
                            // Do NOT clear any existing auth ticket.
                            AppLog.Info("[SteamManager] Bridge returned user info but no auth ticket — will retry next tick.");
                        }
                    }
                    else
                    {
                        // Bridge failed — log the status code for diagnosis and apply backoff.
                        _bridgeFailStreak++;
                        var bridgeStatus = snapshot?.Status ?? "null";
                        AppLog.Error($"[SteamManager] Bridge returned no user info (status={bridgeStatus}, streak={_bridgeFailStreak}).");
                        SetUser(null);
                        SetAuthTicket(null);

                        // Back off exponentially (1s, 2s, 4s, 8s, cap 30s) to avoid hammering Steam API.
                        var backoffSeconds = Math.Min(30, (int)Math.Pow(2, _bridgeFailStreak - 1));
                        if (backoffSeconds > 1)
                        {
                            AppLog.Info($"[SteamManager] Backing off {backoffSeconds}s before next bridge query.");
                            await Task.Delay(TimeSpan.FromSeconds(backoffSeconds - 1), ct); // -1 because the loop adds 1s
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("[SteamManager] MonitorLoop exception.", ex);
                SetSteamStatus(SteamStatus.Offline);
                SetUser(null);
                SetAuthTicket(null);
                _lastActiveUser = 0;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static bool IsSteamProcessRunning()
    {
        var processes = Process.GetProcessesByName("steam");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    private static ulong TryReadActiveUserSteamId()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess", false);
            var raw = key?.GetValue("ActiveUser");
            if (raw == null)
                return 0;

            return raw switch
            {
                int i when i > 0 => (uint)i,
                uint u when u > 0 => u,
                long l when l > 0 => (ulong)l,
                ulong ul when ul > 0 => ul,
                string s when ulong.TryParse(s, out var parsed) => parsed,
                _ => 0
            };
        }
        catch
        {
            return 0;
        }
    }

    private async Task<SteamSnapshot?> QueryBridgeSnapshotAsync(CancellationToken ct)
    {
        var bridgePath = System.IO.Path.Combine(AppContext.BaseDirectory, BridgeExeName);
        if (!System.IO.File.Exists(bridgePath))
        {
            AppLog.Error($"[SteamManager] Bridge not found at: {bridgePath}");
            return null;
        }

        // WorkingDirectory must be the bridge's own directory so SteamAPI.Init()
        // finds steam_appid.txt regardless of the parent process CWD (e.g. Velopack).
        var bridgeDir = System.IO.Path.GetDirectoryName(bridgePath) ?? AppContext.BaseDirectory;
        AppLog.Info($"[SteamManager] Launching bridge: {bridgePath}");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = bridgePath,
                WorkingDirectory = bridgeDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();

        // Read both stdout and stderr asynchronously to prevent pipe-buffer deadlock.
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        // Give bridge up to 12 s (auth ticket callback can take up to 8 s).
        using var killCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        killCts.CancelAfter(TimeSpan.FromSeconds(12));

        try
        {
            await process.WaitForExitAsync(killCts.Token);
        }
        catch (OperationCanceledException)
        {
            AppLog.Error("[SteamManager] Bridge timed out after 12 s — killing process.");
            try { process.Kill(entireProcessTree: true); } catch { }
            return null;
        }

        var output = await outputTask;
        var stderr = await stderrTask;
        if (!string.IsNullOrWhiteSpace(stderr))
            AppLog.Info($"[SteamManager] Bridge stderr: {stderr.Trim()}");

        AppLog.Info($"[SteamManager] Bridge stdout: {(output.Length > 200 ? output[..200] + "..." : output)}");

        if (string.IsNullOrWhiteSpace(output))
        {
            AppLog.Error("[SteamManager] Bridge returned empty output.");
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SteamSnapshot>(output);
        }
        catch (Exception ex)
        {
            AppLog.Error("[SteamManager] Failed to parse bridge output.", ex);
            return null;
        }
    }

    private void SetUser(User? user)
    {
        if (UsersEqual(CurrentUser, user))
            return;

        CurrentUser = user;
        OnUserUpdated?.Invoke(user);
    }

    private void SetSteamStatus(SteamStatus status)
    {
        if (SteamStatus == status)
            return;

        SteamStatus = status;
        OnSteamStatusUpdated?.Invoke(status);
    }

    private void SetAuthTicket(string? ticket)
    {
        if (string.Equals(_steamAuthTicket, ticket, StringComparison.Ordinal))
            return;

        _steamAuthTicket = ticket;
        OnSteamAuthorizationChanged?.Invoke(ticket);
    }

    private static bool UsersEqual(User? a, User? b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a is null || b is null)
            return false;
        if (a.SteamId != b.SteamId
            || !string.Equals(a.PersonaName, b.PersonaName, StringComparison.Ordinal)
            || a.AvatarWidth != b.AvatarWidth
            || a.AvatarHeight != b.AvatarHeight)
            return false;
        if (a.AvatarRgba is null && b.AvatarRgba is null)
            return true;
        if (a.AvatarRgba is null || b.AvatarRgba is null)
            return false;
        if (a.AvatarRgba.Length != b.AvatarRgba.Length)
            return false;

        for (var i = 0; i < a.AvatarRgba.Length; i++)
        {
            if (a.AvatarRgba[i] != b.AvatarRgba[i])
                return false;
        }

        return true;
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        try
        {
            _monitorTask.Wait(500);
        }
        catch
        {
            // Ignore shutdown races.
        }
        _shutdown.Dispose();
    }

    private sealed class SteamSnapshot
    {
        public string? Status { get; set; }
        public ulong? SteamId { get; set; }
        public string? PersonaName { get; set; }
        public byte[]? AvatarRgba { get; set; }
        public int? AvatarWidth { get; set; }
        public int? AvatarHeight { get; set; }
        public string? AuthTicket { get; set; }
    }
}
