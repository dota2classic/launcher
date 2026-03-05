using d2c_launcher.Models;

namespace d2c_launcher.Services;

/// <summary>
/// Singleton service that owns in-memory video settings backed by dota/cfg/video.txt.
/// </summary>
public interface IVideoSettingsProvider
{
    /// <summary>Current in-memory video settings.</summary>
    VideoSettings Get();

    /// <summary>
    /// Update in-memory state and write to video.txt immediately.
    /// </summary>
    void Update(VideoSettings settings);

    /// <summary>
    /// Load video settings from video.txt into memory.
    /// Called on launcher startup and on game exit.
    /// Stores the game directory for subsequent <see cref="Update"/> calls.
    /// </summary>
    void LoadFromVideoTxt(string gameDirectory);
}
