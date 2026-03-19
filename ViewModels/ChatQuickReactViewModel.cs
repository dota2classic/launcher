using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace d2c_launcher.ViewModels;

/// <summary>A single emoticon button in the hover toolbar or picker popup.</summary>
public partial class ChatQuickReactViewModel : ObservableObject
{
    public int EmoticonId { get; }
    /// <summary>GIF stream for display. Null if bytes are not yet available.</summary>
    public MemoryStream? Stream { get; }

    private readonly Func<Task> _react;

    public ChatQuickReactViewModel(int emoticonId, byte[]? gifBytes, Func<Task> react)
    {
        EmoticonId = emoticonId;
        Stream = gifBytes != null ? new MemoryStream(gifBytes) : null;
        _react = react;
    }

    [RelayCommand]
    private Task ReactAsync() => _react();
}
