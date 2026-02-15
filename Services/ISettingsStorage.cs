using d2c_launcher.Models;

namespace d2c_launcher.Services;

public interface ISettingsStorage
{
    LauncherSettings Get();
    void Save(LauncherSettings settings);
}
