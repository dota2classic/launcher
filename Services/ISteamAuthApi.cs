using System.Threading;
using System.Threading.Tasks;

namespace d2c_launcher.Services;

public interface ISteamAuthApi
{
    Task<string?> ExchangeSteamSessionTicketAsync(string ticket, CancellationToken cancellationToken = default);
}
