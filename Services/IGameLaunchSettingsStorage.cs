using d2c_launcher.Models;

namespace d2c_launcher.Services;

public interface IGameLaunchSettingsStorage
{
    GameLaunchSettings Get();
    void Save(GameLaunchSettings settings);
}
