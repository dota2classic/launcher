using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public interface IBackendApiService
{
    /// <summary>Sets the bearer token used for all authenticated API calls. Pass null to clear.</summary>
    void SetBearerToken(string? token);
    Task<PartySnapshot> GetMyPartySnapshotAsync(CancellationToken cancellationToken = default);
    PartySnapshot MapPartyDto(d2c_launcher.Api.PartyDto party);
    Task<IReadOnlyList<MatchmakingModeInfo>> GetEnabledMatchmakingModesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InviteCandidateView>> SearchPlayersAsync(string name, int count = 25, CancellationToken cancellationToken = default);
    Task<(string? Name, string? AvatarUrl)?> GetUserInfoAsync(string steamId, CancellationToken cancellationToken = default);
    Task<(int InGame, int OnSite)> GetOnlineStatsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessageData>> GetChatMessagesAsync(string threadId, int limit, CancellationToken cancellationToken = default);
    Task PostChatMessageAsync(string threadId, string content, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ChatMessageData> SubscribeChatAsync(string threadId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmoticonData>> GetEmoticonsAsync(CancellationToken cancellationToken = default);
    /// <summary>Returns the live match with the given ID, or null if not found.</summary>
    Task<LiveMatchInfo?> GetLiveMatchAsync(int matchId, CancellationToken cancellationToken = default);
    Task<PlayerProfileData?> GetPlayerSummaryAsync(string steamId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HeroProfileData>> GetHeroStatsAsync(string steamId, CancellationToken cancellationToken = default);
    Task AbandonGameAsync(CancellationToken cancellationToken = default);
}
