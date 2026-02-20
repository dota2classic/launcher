using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using d2c_launcher.Api;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public sealed class BackendApiService : IBackendApiService, IDisposable
{
    private static readonly Uri BaseUri = new("https://api.dotaclassic.ru/");
    // Single shared client for requests that don't require per-request auth (modes, etc.)
    private readonly HttpClient _httpClient = new HttpClient
    {
        BaseAddress = BaseUri,
        Timeout = TimeSpan.FromSeconds(10)
    };
    private static readonly IReadOnlyDictionary<int, string> MatchmakingModeLabels = new Dictionary<int, string>
    {
        [0] = "Рейтинговая 5x5",
        [1] = "Обычная 5x5",
        [2] = "1x1 Мид",
        [3] = "Diretide",
        [4] = "Greeviling",
        [5] = "Ability Draft",
        [6] = "Турнир",
        [7] = "Против ботов",
        [8] = "Highroom 5x5",
        [9] = "Турнир 1x1",
        [10] = "Captains Mode",
        [11] = "Лобби",
        [12] = "2x2 с ботами",
        [13] = "Турбо"
    };

    public async Task<PartySnapshot> GetMyPartySnapshotAsync(string bearerToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            AppLog.Info("Party fetch skipped: backend token is empty.");
            return new PartySnapshot(Array.Empty<PartyMemberView>(), null);
        }

        using var httpClient = new HttpClient
        {
            BaseAddress = BaseUri,
            Timeout = TimeSpan.FromSeconds(10)
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var api = new DotaclassicApiClient(httpClient);
        var party = await api.PlayerController_myPartyAsync(cancellationToken);
        if (party == null)
        {
            AppLog.Info("Party fetch returned null party object.");
            return new PartySnapshot(Array.Empty<PartyMemberView>(), null);
        }

        var map = new Dictionary<string, PartyMemberView>(StringComparer.Ordinal);

        if (party.Leader != null && !string.IsNullOrWhiteSpace(party.Leader.SteamId))
        {
            var leaderAvatarUrl = party.Leader.AvatarSmall ?? party.Leader.Avatar;
            var leaderAvatar = await TryLoadAvatarAsync(httpClient, leaderAvatarUrl, cancellationToken);
            map[party.Leader.SteamId] = new PartyMemberView(
                party.Leader.SteamId,
                party.Leader.Name ?? "",
                leaderAvatarUrl,
                leaderAvatar);
        }

        if (party.Players != null)
        {
            foreach (var p in party.Players)
            {
                var summary = p?.Summary;
                var user = summary?.User;
                if (user == null || string.IsNullOrWhiteSpace(user.SteamId))
                    continue;

                var avatarUrl = user.AvatarSmall ?? user.Avatar;
                var avatar = await TryLoadAvatarAsync(httpClient, avatarUrl, cancellationToken);

                var ban = summary.BanStatus;
                var access = summary.AccessMap;
                map[user.SteamId] = new PartyMemberView(
                    user.SteamId,
                    user.Name ?? "",
                    avatarUrl,
                    avatar,
                    isBanned: ban?.IsBanned ?? false,
                    bannedUntil: ban?.BannedUntil,
                    canPlayHumanGames: access?.HumanGames ?? true,
                    canPlaySimpleModes: access?.SimpleModes ?? true,
                    canPlayEducation: access?.Education ?? true);
            }
        }

        DateTimeOffset? enterQueueAt = null;
        if (!string.IsNullOrWhiteSpace(party.EnterQueueAt) &&
            DateTimeOffset.TryParse(
                party.EnterQueueAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            enterQueueAt = parsed;
        }

        var result = map.Values.ToList();
        AppLog.Info($"Party fetch success. Members: {result.Count}");
        return new PartySnapshot(result, enterQueueAt);
    }

    public async Task<IReadOnlyList<MatchmakingModeInfo>> GetEnabledMatchmakingModesAsync(CancellationToken cancellationToken = default)
    {
        var api = new DotaclassicApiClient(_httpClient);
        var modes = await api.StatsController_getMatchmakingInfoAsync(cancellationToken);
        if (modes == null)
            return Array.Empty<MatchmakingModeInfo>();

        var result = new List<MatchmakingModeInfo>();
        foreach (var mode in modes)
        {
            if (mode == null || !mode.Enabled)
                continue;

            var id = (int)mode.Lobby_type;
            var label = MatchmakingModeLabels.TryGetValue(id, out var name)
                ? name
                : $"Mode {id}";
            result.Add(new MatchmakingModeInfo(id, label));
        }

        return result;
    }

    public async Task<IReadOnlyList<InviteCandidateView>> SearchPlayersAsync(string name, int count = 25, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Array.Empty<InviteCandidateView>();

        using var httpClient = new HttpClient
        {
            BaseAddress = BaseUri,
            Timeout = TimeSpan.FromSeconds(10)
        };

        AppLog.Info($"Searching players: name='{name}', count={count}");
        var api = new DotaclassicApiClient(httpClient);
        var users = await api.PlayerController_searchAsync(name, count, cancellationToken);
        if (users == null)
            return Array.Empty<InviteCandidateView>();

        var result = new List<InviteCandidateView>();
        foreach (var user in users)
        {
            if (user == null)
                continue;

            if (string.IsNullOrWhiteSpace(user.SteamId))
                continue;

            var displayName = !string.IsNullOrWhiteSpace(user.Name) ? user.Name : user.SteamId;
            if (string.IsNullOrWhiteSpace(displayName))
                continue;

            var initials = GetInitials(displayName);
            result.Add(new InviteCandidateView(user.SteamId, displayName, initials, false));
        }

        AppLog.Info($"Search returned {result.Count} players.");
        _ = LoadInviteAvatarsAsync(result, users, cancellationToken);
        return result;
    }

    public async Task<(string? Name, Bitmap? AvatarImage)?> GetUserInfoAsync(string steamId, string bearerToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(steamId) || string.IsNullOrWhiteSpace(bearerToken))
            return null;

        try
        {
            using var authClient = new HttpClient
            {
                BaseAddress = BaseUri,
                Timeout = TimeSpan.FromSeconds(5)
            };
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            var api = new DotaclassicApiClient(authClient);
            var user = await api.PlayerController_userAsync(steamId, cancellationToken).ConfigureAwait(false);
            if (user == null)
                return null;

            var avatarUrl = user.AvatarSmall ?? user.Avatar;
            var avatar = await TryLoadAvatarAsync(authClient, avatarUrl, cancellationToken).ConfigureAwait(false);
            return (user.Name, avatar);
        }
        catch
        {
            return null;
        }
    }

    private async Task LoadInviteAvatarsAsync(
        IReadOnlyList<InviteCandidateView> views,
        IEnumerable<UserDTO> users,
        CancellationToken cancellationToken)
    {
        try
        {
            var viewList = views.ToList();
            var userList = users.ToList();
            var count = Math.Min(viewList.Count, userList.Count);

            for (var i = 0; i < count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var user = userList[i];
                var avatarUrl = user?.AvatarSmall ?? user?.Avatar;
                if (string.IsNullOrWhiteSpace(avatarUrl))
                    continue;

                var avatar = await TryLoadAvatarAsync(_httpClient, avatarUrl, cancellationToken);
                if (avatar == null)
                    continue;

                var view = viewList[i];
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    view.AvatarImage?.Dispose();
                    view.AvatarImage = avatar;
                });
            }
        }
        catch
        {
            // Ignore avatar load failures.
        }
    }

    public Task<Bitmap?> LoadAvatarFromUrlAsync(string? url, CancellationToken cancellationToken = default)
        => TryLoadAvatarAsync(_httpClient, url, cancellationToken);

    public async Task<(int InGame, int OnSite)> GetOnlineStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse manually: the API returns inGame as a JSON string ("2"), not a number,
            // so the generated client's double field silently gives 0.
            var json = await _httpClient.GetStringAsync("v1/stats/online", cancellationToken).ConfigureAwait(false);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            // inGame may be a string or a number
            var inGame = 0;
            if (root.TryGetProperty("inGame", out var inGameEl))
            {
                if (inGameEl.ValueKind == System.Text.Json.JsonValueKind.String)
                    int.TryParse(inGameEl.GetString(), out inGame);
                else if (inGameEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                    inGame = inGameEl.GetInt32();
            }

            var onSite = root.TryGetProperty("sessions", out var sessionsEl) ? sessionsEl.GetInt32() : 0;

            return (inGame, onSite);
        }
        catch
        {
            return (0, 0);
        }
    }

    public void Dispose() => _httpClient.Dispose();

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "?";
        if (parts.Length == 1)
            return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpperInvariant() : parts[0].ToUpperInvariant();
        return (parts[0][0].ToString() + parts[^1][0]).ToUpperInvariant();
    }

    private static async Task<Bitmap?> TryLoadAvatarAsync(HttpClient httpClient, string? avatarUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
            return null;

        try
        {
            var uri = new Uri(avatarUrl, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
                uri = new Uri(BaseUri, uri);

            using var response = await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
