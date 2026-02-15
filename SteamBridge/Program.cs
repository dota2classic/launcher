using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using Steamworks;

namespace d2c_steam_bridge;

internal static class Program
{
    private static readonly TimeSpan AuthTicketTimeout = TimeSpan.FromSeconds(3);
    private const string DefaultWebApiIdentity = "dotaclassic.ru";
    private const string WebApiIdentityEnvVar = "D2C_STEAM_WEBAPI_IDENTITY";
    private static Callback<GetTicketForWebApiResponse_t>? _getTicketResponse;
    private static ManualResetEventSlim? _ticketReady;
    private static string? _ticketHex;
    private static HAuthTicket _ticketHandle;

    private static int Main(string[] args)
    {
        try
        {
            if (!SteamAPI.IsSteamRunning())
            {
                WriteSnapshot(new Snapshot("NotRunning"));
                return 0;
            }

            if (!SteamAPI.Init())
            {
                WriteSnapshot(new Snapshot("Offline"));
                return 0;
            }

            try
            {
                _getTicketResponse = Callback<GetTicketForWebApiResponse_t>.Create(OnAuthTicketResponse);
                SteamAPI.RunCallbacks();
                if (!SteamUser.BLoggedOn())
                {
                    WriteSnapshot(new Snapshot("Offline"));
                    return 0;
                }

                var steamIdObj = SteamUser.GetSteamID();
                var steamId = steamIdObj.m_SteamID;
                var personaName = SteamFriends.GetPersonaName();
                TryGetAvatar(steamIdObj, out var avatarRgba, out var avatarWidth, out var avatarHeight);
                var authTicket = TryGetAuthTicket();
                WriteSnapshot(new Snapshot("Running", steamId, personaName, avatarRgba, avatarWidth, avatarHeight, authTicket));
                return 0;
            }
            finally
            {
                SteamAPI.Shutdown();
            }
        }
        catch
        {
            WriteSnapshot(new Snapshot("Offline"));
            return 0;
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

    private static string? TryGetAuthTicket()
    {
        _ticketReady?.Dispose();
        _ticketReady = new ManualResetEventSlim(false);
        _ticketHex = null;
        _ticketHandle = HAuthTicket.Invalid;

        
        var identity = DefaultWebApiIdentity;

        var handle = SteamUser.GetAuthTicketForWebApi(identity);
        
        if (handle == HAuthTicket.Invalid)
            return null;

        _ticketHandle = handle;
        try
        {
            var deadline = DateTime.UtcNow + AuthTicketTimeout;
            while (DateTime.UtcNow < deadline && !_ticketReady.IsSet)
            {
                SteamAPI.RunCallbacks();
                Thread.Sleep(10);
            }

            return _ticketHex;
        }
        finally
        {
            SteamUser.CancelAuthTicket(handle);
            _ticketReady?.Dispose();
            _ticketReady = null;
        }
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
        Console.WriteLine(JsonSerializer.Serialize(snapshot));
    }

    private sealed record Snapshot(
        string Status,
        ulong? SteamId = null,
        string? PersonaName = null,
        byte[]? AvatarRgba = null,
        int AvatarWidth = 0,
        int AvatarHeight = 0,
        string? AuthTicket = null);
}
