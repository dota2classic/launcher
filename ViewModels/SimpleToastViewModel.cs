namespace d2c_launcher.ViewModels;

/// <summary>A plain text toast that auto-dismisses after a given number of seconds.</summary>
public sealed class SimpleToastViewModel : NotificationViewModel
{
    public string Message { get; }

    public SimpleToastViewModel(string message, int displaySeconds = 4)
        : base(displaySeconds)
    {
        Message = message;
    }
}
