using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace d2c_launcher.ViewModels;

public partial class ChatReactionViewModel : ObservableObject
{
    public int EmoticonId { get; }
    /// <summary>Single stream created once from cached GIF bytes. Null if bytes are not available.</summary>
    public MemoryStream? EmoticonStream { get; }

    [ObservableProperty] private int _count;
    [ObservableProperty] private bool _isMine;

    private readonly Func<Task> _react;

    public ChatReactionViewModel(int emoticonId, byte[]? emoticonBytes, int count, bool isMine, Func<Task> react)
    {
        EmoticonId = emoticonId;
        EmoticonStream = emoticonBytes != null ? new MemoryStream(emoticonBytes) : null;
        _count = count;
        _isMine = isMine;
        _react = react;
    }

    [RelayCommand]
    private Task ReactAsync() => _react();
}
