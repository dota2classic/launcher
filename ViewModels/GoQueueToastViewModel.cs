namespace d2c_launcher.ViewModels;

/// <summary>Toast shown when the server asks the player to enter a queue (GO_QUEUE event).</summary>
public sealed class GoQueueToastViewModel : NotificationViewModel
{
    public string Title { get; }
    public string Content { get; }

    public GoQueueToastViewModel(string title, string content, int displaySeconds = 30)
        : base(displaySeconds)
    {
        Title = title;
        Content = content;
    }
}
