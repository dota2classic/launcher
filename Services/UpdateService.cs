using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace d2c_launcher.Services;

public class UpdateService
{
    private readonly UpdateManager _mgr;
    private UpdateInfo? _pendingUpdate;

    public UpdateService(ISettingsStorage settings)
    {
        var isNightly = settings.Get().NightlyUpdates;
        IUpdateSource source = isNightly
            ? new SimpleWebSource("https://github.com/dota2classic/launcher/releases/download/nightly/")
            : new GithubSource("https://github.com/dota2classic/launcher", null, false);

        var options = isNightly
            ? new UpdateOptions { ExplicitChannel = "nightly", AllowVersionDowngrade = true }
            : null;

        _mgr = new UpdateManager(source, options);
    }

    /// <summary>
    /// Checks for updates and downloads them in the background.
    /// Returns (true, notes) if an update was found and is ready to apply.
    /// Safe to call outside of a Velopack-installed context (returns false).
    /// </summary>
    public async Task<(bool HasUpdate, string? ReleaseNotes)> CheckAndDownloadAsync()
    {
        try
        {
            var update = await _mgr.CheckForUpdatesAsync();
            if (update == null) return (false, null);

            await _mgr.DownloadUpdatesAsync(update);
            _pendingUpdate = update;
            return (true, update.TargetFullRelease.NotesMarkdown);
        }
        catch (Exception)
        {
            return (false, null);
        }
    }

    public void ApplyAndRestart()
    {
        if (_pendingUpdate != null)
            _mgr.ApplyUpdatesAndRestart(_pendingUpdate);
    }
}
