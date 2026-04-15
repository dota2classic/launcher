namespace d2c_launcher.Services;

public interface IDotakeysProfileService
{
    /// <summary>
    /// Runs one-time migration (dotakeys_personal.lst → dotakeys_684.lst) if needed,
    /// then patches the required ScreenshotSettings fields.
    /// Returns <c>false</c> if the profile could not be prepared; the caller should log
    /// and proceed — a missing/broken keybind profile must not block game launch.
    /// </summary>
    bool PrepareProfile(ulong steamId32);
}
