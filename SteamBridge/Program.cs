using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Steamworks;

namespace d2c_steam_bridge;

internal static class Program
{
    private static readonly TimeSpan AuthTicketTimeout = TimeSpan.FromSeconds(8);
    private const string DefaultWebApiIdentity = "dotaclassic.ru";
    private const string WebApiIdentityEnvVar = "D2C_STEAM_WEBAPI_IDENTITY";
    private const string BackendBaseUrl = "https://api.dotaclassic.ru/";
    private static Callback<GetTicketForWebApiResponse_t>? _getTicketResponse;
    private static ManualResetEventSlim? _ticketReady;
    private static string? _ticketHex;
    private static HAuthTicket _ticketHandle;

    private static string ParseHwid(string[] args)
    {
        var idx = Array.IndexOf(args, "--hwid");
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : "unknown";
    }

    private static int Main(string[] args)
    {
        var hwid = ParseHwid(args);
        try
        {
            if (!SteamAPI.IsSteamRunning())
            {
                Log("info", "Steam is not running.");
                WriteSnapshot(new Snapshot("NotRunning"));
                return 0;
            }

            bool initOk = false;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (SteamAPI.Init())
                {
                    initOk = true;
                    break;
                }
                if (attempt < 2)
                {
                    Log("warn", $"SteamAPI.Init() failed (attempt {attempt + 1}/3), retrying in 1 s...");
                    Thread.Sleep(1000);
                }
            }
            if (!initOk)
            {
                Log("error", "SteamAPI.Init() failed after 3 attempts — Steam is running but init returned false.");
                WriteSnapshot(new Snapshot("InitFailed"));
                return 0;
            }

            try
            {
                _getTicketResponse = Callback<GetTicketForWebApiResponse_t>.Create(OnAuthTicketResponse);
                SteamAPI.RunCallbacks();
                if (!SteamUser.BLoggedOn())
                {
                    Log("info", "Steam is running but the user is not logged in.");
                    WriteSnapshot(new Snapshot("NotLoggedIn"));
                    return 0;
                }

                var steamIdObj = SteamUser.GetSteamID();
                var steamId = steamIdObj.m_SteamID;
                var personaName = SteamFriends.GetPersonaName();
                TryGetAvatar(steamIdObj, out var avatarRgba, out var avatarWidth, out var avatarHeight);

                // Get ticket and immediately exchange it with the backend while SteamAPI is still running.
                var ticketHex = TryGetAuthTicket(out var authTicketStatus);
                if (ticketHex == null)
                {
                    Log("warn", $"Auth ticket not obtained: {authTicketStatus}.");
                    WriteSnapshot(new Snapshot(authTicketStatus, steamId, personaName, avatarRgba, avatarWidth, avatarHeight));
                    return 0;
                }

                var backendToken = ExchangeForBackendToken(ticketHex, hwid);
                if (backendToken == null)
                    Log("warn", "Backend token exchange failed — API call returned null.");

                Log("info", $"Snapshot ready: status=Running, steamId={steamId}, hasToken={backendToken != null}.");
                WriteSnapshot(new Snapshot("Running", steamId, personaName, avatarRgba, avatarWidth, avatarHeight, backendToken));
                return 0;
            }
            finally
            {
                SteamAPI.Shutdown();
            }
        }
        catch (Exception ex)
        {
            Log("error", $"Unhandled exception: {ex.GetType().Name}: {ex.Message}");
            WriteSnapshot(new Snapshot("Offline"));
            return 0;
        }
    }

    private static string? ExchangeForBackendToken(string ticketHex, string hwid)
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(BackendBaseUrl), Timeout = TimeSpan.FromSeconds(4) };
            var url = "v1/auth/steam/steam_session_ticket?ticket=" + Uri.EscapeDataString(ticketHex)
                      + "&hwid=" + Uri.EscapeDataString(hwid);
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            using var response = http.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                Log("warn", $"Backend token exchange HTTP error: {(int)response.StatusCode} {response.StatusCode}.");
                return null;
            }

            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(body))
            {
                Log("warn", "Backend token exchange returned empty body.");
                return null;
            }

            try { return JsonSerializer.Deserialize<string>(body); }
            catch { return body.Trim().Trim('"'); }
        }
        catch (Exception ex)
        {
            Log("error", $"Backend token exchange threw: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static void TryGetAvatar(CSteamID steamId, out byte[]? rgba, out int width, out int height)
    {
        rgba = null;
        width = 0;
        height = 0;

        var imageHandle = GetAvatarHandle(steamId);
        if (imageHandle <= 0)
            return;

        if (!SteamUtils.GetImageSize(imageHandle, out var w, out var h) || w == 0 || h == 0)
            return;

        var imageBytes = (int)(w * h * 4);
        var buffer = new byte[imageBytes];
        if (!SteamUtils.GetImageRGBA(imageHandle, buffer, imageBytes))
            return;

        rgba = buffer;
        width = (int)w;
        height = (int)h;
    }

    private static int GetAvatarHandle(CSteamID steamId)
    {
        for (var i = 0; i < 8; i++)
        {
            var handle = TryGetAvatarHandleOnce(steamId);
            if (handle > 0)
                return handle;

            if (handle != -1)
                return -1;

            SteamAPI.RunCallbacks();
            Thread.Sleep(50);
        }

        return -1;
    }

    private static int TryGetAvatarHandleOnce(CSteamID steamId)
    {
        var large = SteamFriends.GetLargeFriendAvatar(steamId);
        if (large > 0)
            return large;

        var medium = SteamFriends.GetMediumFriendAvatar(steamId);
        if (medium > 0)
            return medium;

        var small = SteamFriends.GetSmallFriendAvatar(steamId);
        if (small > 0)
            return small;

        if (large == -1 || medium == -1 || small == -1)
            return -1;

        return 0;
    }

    private static string? TryGetAuthTicket(out string failStatus)
    {
        _ticketReady?.Dispose();
        _ticketReady = new ManualResetEventSlim(false);
        _ticketHex = null;
        _ticketHandle = HAuthTicket.Invalid;

        var handle = SteamUser.GetAuthTicketForWebApi(DefaultWebApiIdentity);
        if (handle == HAuthTicket.Invalid)
        {
            failStatus = "AuthTicketFailed";
            return null;
        }

        _ticketHandle = handle;
        var deadline = DateTime.UtcNow + AuthTicketTimeout;
        while (DateTime.UtcNow < deadline && !_ticketReady.IsSet)
        {
            SteamAPI.RunCallbacks();
            Thread.Sleep(10);
        }

        _ticketReady?.Dispose();
        _ticketReady = null;

        if (_ticketHex == null)
        {
            failStatus = "AuthTicketTimeout";
            return null;
        }

        failStatus = "Running";
        return _ticketHex;
    }

    private static void OnAuthTicketResponse(GetTicketForWebApiResponse_t response)
    {
        if (_ticketHandle == HAuthTicket.Invalid || response.m_hAuthTicket != _ticketHandle)
            return;

        if (response.m_eResult != EResult.k_EResultOK || response.m_cubTicket == 0)
        {
            _ticketHex = null;
            _ticketReady?.Set();
            return;
        }

        _ticketHex = Convert.ToHexString(response.m_rgubTicket, 0, response.m_cubTicket).ToLowerInvariant();
        _ticketReady?.Set();
    }

    private static void WriteSnapshot(Snapshot snapshot)
    {
        Console.WriteLine(JsonSerializer.Serialize(snapshot, SnapshotJsonContext.Default.Snapshot));
    }

    /// <summary>
    /// Writes a structured log line to stderr. The main process reads bridge stderr
    /// and forwards it to AppLog (and thus to Faro). Format: [LEVEL] message
    /// </summary>
    private static void Log(string level, string message)
    {
        Console.Error.WriteLine($"[{level.ToUpperInvariant()}] {message}");
    }

}

internal sealed record Snapshot(
    string Status,
    ulong? SteamId = null,
    string? PersonaName = null,
    byte[]? AvatarRgba = null,
    int AvatarWidth = 0,
    int AvatarHeight = 0,
    string? BackendToken = null);

[JsonSerializable(typeof(Snapshot))]
internal partial class SnapshotJsonContext : JsonSerializerContext { }
