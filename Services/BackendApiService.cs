using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using d2c_launcher.Api;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public sealed class BackendApiService : IBackendApiService, IDisposable
{
    // TODO: Read base URL from an environment variable (e.g. D2C_API_URL) for parity with
    // QueueSocketService, which already uses D2C_SOCKET_URL. Hardcoding makes staging impossible.
    private static readonly Uri BaseUri = new("https://api.dotaclassic.ru/");
    // Shared client for unauthenticated requests (modes, online stats, avatar loading).
    private readonly HttpClient _httpClient = new HttpClient
    {
        BaseAddress = BaseUri,
        Timeout = TimeSpan.FromSeconds(10)
    };
    // Long-lived client for SSE streams — no timeout (cancelled via CancellationToken).
    private readonly HttpClient _sseHttpClient = new HttpClient
    {
        BaseAddress = BaseUri,
        Timeout = System.Threading.Timeout.InfiniteTimeSpan
    };
    // TODO: Merge into a single HttpClient. Setting DefaultRequestHeaders per-call is not
    // thread-safe under concurrent requests. Prefer passing the bearer token via a delegating
    // handler or creating a new HttpRequestMessage per call.
    // Shared client for authenticated requests. Auth header is set per-call before use.
    // Thread-safe for our single-user desktop app (only one token in flight at a time).
    private readonly HttpClient _authHttpClient = new HttpClient
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

        _authHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var api = new DotaclassicApiClient(_authHttpClient);
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
            var leaderAvatar = await TryLoadAvatarAsync(_httpClient, leaderAvatarUrl, cancellationToken);
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
                var avatar = await TryLoadAvatarAsync(_httpClient, avatarUrl, cancellationToken);

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

        AppLog.Info($"Searching players: name='{name}', count={count}");
        var api = new DotaclassicApiClient(_httpClient);
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
            _authHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            var api = new DotaclassicApiClient(_authHttpClient);
            var user = await api.PlayerController_userAsync(steamId, cancellationToken).ConfigureAwait(false);
            if (user == null)
                return null;

            var avatarUrl = user.AvatarSmall ?? user.Avatar;
            var avatar = await TryLoadAvatarAsync(_httpClient, avatarUrl, cancellationToken).ConfigureAwait(false);
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

    public async Task<IReadOnlyList<ChatMessageData>> GetChatMessagesAsync(
        string threadId, int limit, string bearerToken, CancellationToken cancellationToken = default)
    {
        _authHttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);
        var api = new DotaclassicApiClient(_authHttpClient);
        var messages = await api.ForumController_getMessagesAsync(
            threadId,
            threadType: Api.ThreadType.Forum,
            cursor: null,
            limit: limit,
            order: Api.SortOrder.DESC,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (messages == null)
            return Array.Empty<ChatMessageData>();

        var result = new List<ChatMessageData>(messages.Count);
        foreach (var msg in messages)
        {
            if (msg == null || msg.Deleted)
                continue;
            result.Add(new ChatMessageData(
                msg.MessageId,
                msg.ThreadId,
                msg.Content,
                msg.CreatedAt,
                msg.Author?.SteamId ?? "",
                msg.Author?.Name ?? "",
                msg.Author?.AvatarSmall ?? msg.Author?.Avatar,
                msg.Deleted));
        }
        return result;
    }

    public async Task PostChatMessageAsync(
        string threadId, string content, string bearerToken, CancellationToken cancellationToken = default)
    {
        _authHttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);
        var api = new DotaclassicApiClient(_authHttpClient);
        // The POST body expects the full prefixed threadId (e.g. "forum_<uuid>"),
        // while the GET endpoints use the bare UUID + a separate threadType param.
        await api.ForumController_postMessageAsync(
            new Api.CreateMessageDTO { ThreadId = $"forum_{threadId}", Content = content },
            cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<ChatMessageData> SubscribeChatAsync(
        string threadId, string bearerToken,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var url = $"v1/forum/thread/{Uri.EscapeDataString(threadId)}/forum/sse";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _sseHttpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        var dataBuffer = new StringBuilder();
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line == null) break; // stream ended

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var payload = line.Length > 5 ? line.Substring(5).TrimStart() : "";
                dataBuffer.Append(payload);
            }
            else if (line.Length == 0 && dataBuffer.Length > 0)
            {
                // Blank line = end of SSE event
                var json = dataBuffer.ToString();
                dataBuffer.Clear();
                var msg = ParseSseChatMessage(json);
                if (msg != null)
                    yield return msg;
            }
        }
    }

    private static ChatMessageData? ParseSseChatMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var messageId = root.TryGetProperty("messageId", out var mid) ? mid.GetString() ?? "" : "";
            var deleted = root.TryGetProperty("deleted", out var del) && del.GetBoolean();

            if (deleted)
                return new ChatMessageData(messageId, "", "", "", "", "", null, true);

            var author = root.TryGetProperty("author", out var authorEl) ? authorEl : default;
            return new ChatMessageData(
                MessageId: messageId,
                ThreadId: root.TryGetProperty("threadId", out var tid) ? tid.GetString() ?? "" : "",
                Content: root.TryGetProperty("content", out var content) ? content.GetString() ?? "" : "",
                CreatedAt: root.TryGetProperty("createdAt", out var created) ? created.GetString() ?? "" : "",
                AuthorSteamId: author.ValueKind != JsonValueKind.Undefined
                    && author.TryGetProperty("steamId", out var sid) ? sid.GetString() ?? "" : "",
                AuthorName: author.ValueKind != JsonValueKind.Undefined
                    && author.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                AuthorAvatarUrl: author.ValueKind != JsonValueKind.Undefined
                    && author.TryGetProperty("avatarSmall", out var avatar) ? avatar.GetString() : null,
                Deleted: false);
        }
        catch (Exception ex)
        {
            AppLog.Error($"Chat SSE parse failed: {ex.Message}", ex);
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _authHttpClient.Dispose();
        _sseHttpClient.Dispose();
    }

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
