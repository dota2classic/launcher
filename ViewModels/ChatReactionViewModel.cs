using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace d2c_launcher.ViewModels;

public partial class ChatReactionViewModel : ObservableObject
{
    public int EmoticonId { get; }
    public string EmoticonCode { get; }
    /// <summary>Raw emoticon bytes passed directly to <c>EmoticonImage</c>.</summary>
    [ObservableProperty] private byte[]? _emoticonBytes;

    [ObservableProperty] private int _count;
    [ObservableProperty] private bool _isMine;

    private readonly Func<Task> _react;

    public ChatReactionViewModel(int emoticonId, string emoticonCode, byte[]? emoticonBytes, int count, bool isMine, Func<Task> react)
    {
        EmoticonId = emoticonId;
        EmoticonCode = emoticonCode;
        _emoticonBytes = emoticonBytes;
        _count = count;
        _isMine = isMine;
        _react = react;
    }

    [RelayCommand]
    private Task ReactAsync() => _react();
}
