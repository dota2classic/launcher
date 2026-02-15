using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using d2c_launcher.Api;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public sealed class BackendApiService : IBackendApiService
{
    private static readonly Uri BaseUri = new("https://api.dotaclassic.ru/");
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

    public async Task<IReadOnlyList<PartyMemberView>> GetMyPartyAsync(string bearerToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            AppLog.Info("Party fetch skipped: backend token is empty.");
            return Array.Empty<PartyMemberView>();
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
            return Array.Empty<PartyMemberView>();
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
                var user = p?.Summary?.User;
                if (user == null || string.IsNullOrWhiteSpace(user.SteamId))
                    continue;

                var avatarUrl = user.AvatarSmall ?? user.Avatar;
                var avatar = await TryLoadAvatarAsync(httpClient, avatarUrl, cancellationToken);
                map[user.SteamId] = new PartyMemberView(
                    user.SteamId,
                    user.Name ?? "",
                    avatarUrl,
                    avatar);
            }
        }

        var result = map.Values.ToList();
        AppLog.Info($"Party fetch success. Members: {result.Count}");
        return result;
    }

    public async Task<IReadOnlyList<MatchmakingModeInfo>> GetEnabledMatchmakingModesAsync(CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = BaseUri,
            Timeout = TimeSpan.FromSeconds(10)
        };

        var api = new DotaclassicApiClient(httpClient);
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
