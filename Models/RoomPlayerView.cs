using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Services;

namespace d2c_launcher.Models;

public sealed partial class RoomPlayerView : ObservableObject
{
    public string SteamId { get; }
    public string Name { get; }
    public string? AvatarUrl { get; }

    [ObservableProperty]
    private ReadyState _state;

    public RoomPlayerView(string steamId, string name, string? avatarUrl, ReadyState state)
    {
        SteamId = steamId;
        Name = name;
        AvatarUrl = avatarUrl;
        _state = state;
    }

    public string Initials => Name.Length > 0 ? Name[0].ToString().ToUpperInvariant() : "?";

    public string StateText => State switch
    {
        ReadyState.Ready => "✓ Готов",
        ReadyState.Decline => "✗ Отклонил",
        ReadyState.Timeout => "⏱ Время вышло",
        ReadyState.Pending => "⏳ Ожидание...",
        _ => "?"
    };

    public IBrush StateColor => State switch
    {
        ReadyState.Ready => new SolidColorBrush(Color.Parse("#44D46F")),
        ReadyState.Decline => new SolidColorBrush(Color.Parse("#E74C3C")),
        ReadyState.Timeout => new SolidColorBrush(Color.Parse("#95A5A6")),
        ReadyState.Pending => new SolidColorBrush(Color.Parse("#F39C12")),
        _ => new SolidColorBrush(Colors.Gray)
    };

    partial void OnStateChanged(ReadyState value)
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(StateColor));
    }
}
