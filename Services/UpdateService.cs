using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace d2c_launcher.Services;

public class UpdateService
{
    private readonly UpdateManager _mgr;
    private UpdateInfo? _pendingUpdate;

    public UpdateService()
    {
        _mgr = new UpdateManager(new GithubSource("https://github.com/dota2classic/launcher", null, false));
    }

    /// <summary>
    /// Checks for updates and downloads them in the background.
    /// Returns true if an update was found and is ready to apply.
    /// Safe to call outside of a Velopack-installed context (returns false).
    /// </summary>
    public async Task<bool> CheckAndDownloadAsync()
    {
        try
        {
            var update = await _mgr.CheckForUpdatesAsync();
            if (update == null) return false;

            await _mgr.DownloadUpdatesAsync(update);
            _pendingUpdate = update;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void ApplyAndRestart()
    {
        if (_pendingUpdate != null)
            _mgr.ApplyUpdatesAndRestart(_pendingUpdate);
    }
}
