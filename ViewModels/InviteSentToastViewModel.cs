namespace d2c_launcher.ViewModels;

/// <summary>Toast shown after successfully inviting a player to the party.</summary>
public sealed class InviteSentToastViewModel : NotificationViewModel
{
    public string PlayerName { get; }
    public string PlayerInitials { get; }
    public string? AvatarUrl { get; }

    public InviteSentToastViewModel(string playerName, string playerInitials, string? avatarUrl, int displaySeconds = 4)
        : base(displaySeconds)
    {
        PlayerName = playerName;
        PlayerInitials = playerInitials;
        AvatarUrl = avatarUrl;
    }
}
