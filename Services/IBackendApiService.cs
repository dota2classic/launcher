using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public interface IBackendApiService
{
    Task<PartySnapshot> GetMyPartySnapshotAsync(string bearerToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchmakingModeInfo>> GetEnabledMatchmakingModesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InviteCandidateView>> SearchPlayersAsync(string name, int count = 25, CancellationToken cancellationToken = default);
    Task<(string? Name, Bitmap? AvatarImage)?> GetUserInfoAsync(string steamId, string bearerToken, CancellationToken cancellationToken = default);
}
