using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public interface IUserNameResolver
{
    /// <summary>
    /// Returns the <see cref="PlayerNameViewModel"/> for the given steamId, creating it if needed
    /// and scheduling an API fetch on first call. The same instance is returned for every subsequent
    /// call with the same steamId — DisplayName updates in-place when the name resolves.
    /// </summary>
    PlayerNameViewModel GetOrCreate(string steamId);
}

public sealed class UserNameResolver : IUserNameResolver
{
    private readonly IBackendApiService _api;
    private readonly IUiDispatcher _dispatcher;
    private readonly Dictionary<string, PlayerNameViewModel> _vms = new(System.StringComparer.Ordinal);

    public UserNameResolver(IBackendApiService api, IUiDispatcher dispatcher)
    {
        _api = api;
        _dispatcher = dispatcher;
    }

    public PlayerNameViewModel GetOrCreate(string steamId)
    {
        if (_vms.TryGetValue(steamId, out var vm))
            return vm;

        vm = new PlayerNameViewModel();
        _vms[steamId] = vm;
        _ = LoadAsync(steamId, vm);
        return vm;
    }

    private async Task LoadAsync(string steamId, PlayerNameViewModel vm)
    {
        try
        {
            var info = await _api.GetUserInfoAsync(steamId).ConfigureAwait(false);
            if (info == null)
            {
                AppLog.Warn($"UserNameResolver: no data for {steamId}");
                return;
            }

            var name = info.Value.Name ?? steamId;
            _dispatcher.Post(() => vm.DisplayName = $"@{name}");
        }
        catch (Exception ex)
        {
            AppLog.Warn($"UserNameResolver: failed to load name for {steamId} — {ex.Message}");
        }
    }
}
