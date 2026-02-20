namespace d2c_launcher.ViewModels;

public partial class LaunchSteamFirstViewModel : ViewModelBase
{
    public string Message { get; }

    public LaunchSteamFirstViewModel(string message = "Сначала запустите Steam.")
    {
        Message = message;
    }
}
