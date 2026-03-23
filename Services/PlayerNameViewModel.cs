using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Resources;

namespace d2c_launcher.Services;

/// <summary>
/// Observable display name for a single player. Starts as "Загрузка..." and updates in-place
/// when the API resolves the name. Shared across all UI elements that show the same steamId —
/// only one API call is made regardless of how many messages reference the same player.
/// </summary>
public partial class PlayerNameViewModel : ObservableObject
{
    [ObservableProperty]
    private string _displayName = Strings.Loading;
}
