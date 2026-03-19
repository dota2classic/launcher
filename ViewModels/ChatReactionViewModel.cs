using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace d2c_launcher.ViewModels;

public partial class ChatReactionViewModel : ObservableObject
{
    public int EmoticonId { get; }
    /// <summary>Raw emoticon bytes passed directly to <c>EmoticonImage</c>.</summary>
    public byte[]? EmoticonBytes { get; }

    [ObservableProperty] private int _count;
    [ObservableProperty] private bool _isMine;

    private readonly Func<Task> _react;

    public ChatReactionViewModel(int emoticonId, byte[]? emoticonBytes, int count, bool isMine, Func<Task> react)
    {
        EmoticonId = emoticonId;
        EmoticonBytes = emoticonBytes;
        _count = count;
        _isMine = isMine;
        _react = react;
    }

    [RelayCommand]
    private Task ReactAsync() => _react();
}
