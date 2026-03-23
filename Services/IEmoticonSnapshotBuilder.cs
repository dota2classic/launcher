using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public interface IEmoticonSnapshotBuilder
{
    /// <summary>Top 3 emoticons for the hover quick-react toolbar.</summary>
    IReadOnlyList<(int Id, string Code, byte[]? GifBytes)> Top3 { get; }

    /// <summary>All emoticons for the picker flyout.</summary>
    IReadOnlyList<(int Id, string Code, byte[]? GifBytes)> All { get; }

    /// <summary>GIF bytes keyed by emoticon code.</summary>
    IReadOnlyDictionary<string, byte[]> Images { get; }

    /// <summary>True after the first successful load.</summary>
    bool IsLoaded { get; }

    /// <summary>Loads emoticons if not already loaded. Safe to call concurrently — loads only once.</summary>
    Task EnsureLoadedAsync();

    /// <summary>Raised on the UI thread once emoticons are loaded and snapshots are built.</summary>
    event Action? SnapshotReady;
}

public sealed class EmoticonSnapshotBuilder : IEmoticonSnapshotBuilder
{
    private readonly IEmoticonService _emoticonService;
    private readonly IUiDispatcher _dispatcher;

    private readonly object _lock = new();
    private Task? _loadTask;

    private Dictionary<string, byte[]> _images = new(StringComparer.Ordinal);
    private IReadOnlyList<(int Id, string Code, byte[]? GifBytes)> _top3 = Array.Empty<(int, string, byte[]?)>();
    private IReadOnlyList<(int Id, string Code, byte[]? GifBytes)> _all = Array.Empty<(int, string, byte[]?)>();

    public IReadOnlyList<(int Id, string Code, byte[]? GifBytes)> Top3 => _top3;
    public IReadOnlyList<(int Id, string Code, byte[]? GifBytes)> All => _all;
    public IReadOnlyDictionary<string, byte[]> Images => _images;
    public bool IsLoaded { get; private set; }

    public event Action? SnapshotReady;

    public EmoticonSnapshotBuilder(IEmoticonService emoticonService, IUiDispatcher dispatcher)
    {
        _emoticonService = emoticonService;
        _dispatcher = dispatcher;
    }

    public Task EnsureLoadedAsync()
    {
        lock (_lock)
        {
            if (_loadTask != null) return _loadTask;
            _loadTask = LoadCoreAsync();
            return _loadTask;
        }
    }

    private async Task LoadCoreAsync()
    {
        // Yield immediately so the caller's `_loadTask = LoadCoreAsync()` assignment
        // completes before this method body runs.  Without this, an already-completed
        // (e.g. faulted) service task causes the entire method — including the catch
        // block's `_loadTask = null` reset — to execute synchronously, which means the
        // reset fires *before* the assignment stores the task, so the assignment then
        // overwrites null with the completed task and the retry guard breaks.
        await Task.Yield();
        try
        {
            var result = await _emoticonService.LoadEmoticonsAsync().ConfigureAwait(false);
            _images = result.Images;

            _all = result.Ordered
                .Select(e => (e.Id, e.Code, GifBytes: _images.GetValueOrDefault(e.Code)))
                .ToList();

            _top3 = _all.Take(3).ToList();

            IsLoaded = true;
            _dispatcher.Post(() => SnapshotReady?.Invoke());
        }
        catch (Exception ex)
        {
            AppLog.Error($"EmoticonSnapshotBuilder: failed to load emoticons: {ex.Message}", ex);
            // Reset so the next call can retry.
            lock (_lock) { _loadTask = null; }
        }
    }
}
