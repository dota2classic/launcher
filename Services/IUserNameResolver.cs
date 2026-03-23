using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public interface IUserNameResolver
{
    /// <summary>Schedules async name loads for any <see cref="PlayerLinkSegment"/> not yet in the cache.</summary>
    void ScheduleLoads(IReadOnlyList<RichSegment> segments);

    /// <summary>Read-only view of the name cache. Value is null while the fetch is in-flight.</summary>
    IReadOnlyDictionary<string, string?> Cache { get; }

    /// <summary>Raised on the UI thread after one or more names are added to the cache.</summary>
    event Action? NamesUpdated;
}

public sealed class UserNameResolver : IUserNameResolver
{
    private readonly IBackendApiService _api;
    private readonly IUiDispatcher _dispatcher;
    private readonly Dictionary<string, string?> _cache = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string?> Cache => _cache;
    public event Action? NamesUpdated;

    public UserNameResolver(IBackendApiService api, IUiDispatcher dispatcher)
    {
        _api = api;
        _dispatcher = dispatcher;
    }

    public void ScheduleLoads(IReadOnlyList<RichSegment> segments)
    {
        foreach (var seg in segments.OfType<PlayerLinkSegment>())
        {
            if (_cache.ContainsKey(seg.SteamId)) continue;
            _cache[seg.SteamId] = null; // mark in-flight
            _ = LoadAsync(seg.SteamId);
        }
    }

    private async Task LoadAsync(string steamId)
    {
        var info = await _api.GetUserInfoAsync(steamId).ConfigureAwait(false);
        if (info == null)
        {
            // No token yet or user not found — remove from cache so it retries next time.
            _cache.Remove(steamId);
            return;
        }

        var name = info.Value.Name ?? steamId;
        _dispatcher.Post(() =>
        {
            _cache[steamId] = name;
            NamesUpdated?.Invoke();
        });
    }
}
