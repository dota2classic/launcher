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
        // Allow a local directory override for manual update testing:
        //   set D2C_UPDATE_SOURCE=C:\vpk-test  before launching the app
        var isNightly = settings.Get().NightlyUpdates;
        var localSource = Environment.GetEnvironmentVariable("D2C_UPDATE_SOURCE");
        IUpdateSource source = string.IsNullOrWhiteSpace(localSource)
            ? new GithubSource("https://github.com/dota2classic/launcher", null, isNightly)
            : new SimpleWebSource("file:///" + localSource.Replace('\\', '/'));

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
