using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace d2c_launcher.ViewModels;

/// <summary>A single emoticon button in the hover toolbar or picker popup.</summary>
public partial class ChatQuickReactViewModel : ObservableObject
{
    public int EmoticonId { get; }
    /// <summary>Raw emoticon bytes passed directly to <c>EmoticonImage</c>.</summary>
    public byte[]? Bytes { get; }

    private readonly Func<Task> _react;

    public ChatQuickReactViewModel(int emoticonId, byte[]? gifBytes, Func<Task> react)
    {
        EmoticonId = emoticonId;
        Bytes = gifBytes;
        _react = react;
    }

    [RelayCommand]
    private Task ReactAsync() => _react();
}
