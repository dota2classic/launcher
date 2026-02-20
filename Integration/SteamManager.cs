using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using d2c_launcher.Models;

namespace d2c_launcher.Integration;

[SupportedOSPlatform("windows")]
public class SteamManager : IDisposable
{
    private const string BridgeExeName = "d2c-steam-bridge.exe";
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _monitorTask;
    private ulong _lastActiveUser;
    private string? _steamAuthTicket;

    public event Action<User?>? OnUserUpdated;
    public event Action<SteamStatus>? OnSteamStatusUpdated;
    public event Action<string?>? OnSteamAuthorizationChanged;

    public User? CurrentUser { get; private set; }
    public SteamStatus SteamStatus { get; private set; } = SteamStatus.NotRunning;

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
                    SetUser(null);
                    SetAuthTicket(null);
                }
                else if (activeUser != 0 && activeUser != _lastActiveUser)
                {
                    var snapshot = QueryBridgeSnapshot();
                    if (snapshot?.SteamId.HasValue == true && !string.IsNullOrWhiteSpace(snapshot.PersonaName))
                    {
                        // Only mark user as loaded after a successful bridge query.
                        _lastActiveUser = activeUser;
                        var user = new User(
                            snapshot.SteamId.Value,
                            snapshot.PersonaName,
                            snapshot.AvatarRgba,
                            snapshot.AvatarWidth ?? 0,
                            snapshot.AvatarHeight ?? 0);
                        SetUser(user);
                        SetAuthTicket(snapshot.AuthTicket);
                    }
                    else
                    {
                        // Bridge failed or returned no user — leave _lastActiveUser unchanged
                        // so the query is retried on the next tick.
                        SetUser(null);
                        SetAuthTicket(null);
                    }
                }
            }
            catch
            {
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

    private SteamSnapshot? QueryBridgeSnapshot()
    {
        var bridgePath = System.IO.Path.Combine(AppContext.BaseDirectory, BridgeExeName);
        if (!System.IO.File.Exists(bridgePath))
            return null;

        // WorkingDirectory must be the bridge's own directory so SteamAPI.Init()
        // finds steam_appid.txt regardless of the parent process CWD (e.g. Velopack).
        var bridgeDir = System.IO.Path.GetDirectoryName(bridgePath) ?? AppContext.BaseDirectory;

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
        var output = process.StandardOutput.ReadToEnd();
        // Auth ticket callback can take up to 3 s; allow 8 s total.
        if (!process.WaitForExit(8000))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return null;
        }

        if (string.IsNullOrWhiteSpace(output))
            return null;

        return JsonSerializer.Deserialize<SteamSnapshot>(output);
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
