using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public interface IBackendApiService
{
    Task<IReadOnlyList<PartyMemberView>> GetMyPartyAsync(string bearerToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchmakingModeInfo>> GetEnabledMatchmakingModesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InviteCandidateView>> SearchPlayersAsync(string name, int count = 25, CancellationToken cancellationToken = default);
}
