using Avalonia.Controls;
using Avalonia.Input;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Views.Components;

public partial class ChatPanel : UserControl
{
    public ChatPanel()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ChatViewModel vm)
        {
            vm.MessagesUpdated += ScrollToBottom;
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ChatViewModel vm)
        {
            e.Handled = true;
            _ = vm.SendMessageCommand.ExecuteAsync(null);
        }
    }

    private void ScrollToBottom()
    {
        MessagesScroll.ScrollToEnd();
    }
}
