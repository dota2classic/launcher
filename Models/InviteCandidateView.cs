using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.Models;

public sealed partial class InviteCandidateView : ObservableObject
{
    public string SteamId { get; }
    public string Name { get; }
    public string Initials { get; }
    public string? AvatarUrl { get; }
    [ObservableProperty]
    private bool _isOnline;

    public InviteCandidateView(string steamId, string name, string initials, bool isOnline, string? avatarUrl = null)
    {
        SteamId = steamId;
        Name = name;
        Initials = initials;
        AvatarUrl = avatarUrl;
        _isOnline = isOnline;
    }
}
