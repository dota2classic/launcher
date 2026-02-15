using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.Models;

public sealed partial class InviteCandidateView : ObservableObject
{
    public string SteamId { get; }
    public string Name { get; }
    public string Initials { get; }
    [ObservableProperty]
    private bool _isOnline;
    [ObservableProperty]
    private Bitmap? _avatarImage;

    public InviteCandidateView(string steamId, string name, string initials, bool isOnline, Bitmap? avatarImage = null)
    {
        SteamId = steamId;
        Name = name;
        Initials = initials;
        _isOnline = isOnline;
        _avatarImage = avatarImage;
    }
}
