using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using d2c_launcher.Models;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Views.Components;

public partial class ChatPanel : UserControl
{
    private ChatViewModel? _vm;

    public ChatPanel()
    {
        InitializeComponent();
        AddHandler(RichMessageBlock.PlayerLinkClickedEvent, OnPlayerLinkClicked);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is { } root)
            root.AddHandler(KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (TopLevel.GetTopLevel(this) is { } root)
            root.RemoveHandler(KeyDownEvent, OnGlobalKeyDown);
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (!MessageInput.IsFocused
            && IsVisible
            && !e.Handled
            && IsTypingKey(e.Key, e.KeyModifiers)
            && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not TextBox)
        {
            MessageInput.Focus();
        }
    }

    private void OnPlayerLinkClicked(object? sender, PlayerLinkClickedEventArgs e)
    {
        if (DataContext is ChatViewModel vm)
            vm.OpenPlayerProfileByIdCommand.Execute(e.Steam32Id);
    }

    private void OnAuthorClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ChatMessageView msg } && DataContext is ChatViewModel vm)
            vm.OpenPlayerProfileByIdCommand.Execute(msg.AuthorSteamId);
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm != null)
            _vm.MessagesUpdated -= ScrollToBottom;
        _vm = DataContext as ChatViewModel;
        if (_vm != null)
            _vm.MessagesUpdated += ScrollToBottom;
    }

    private static bool IsTypingKey(Key key, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Alt))
            return false;
        return key is not (
            Key.None or Key.Escape or Key.Tab or Key.Enter or Key.Back or Key.Delete or
            Key.Left or Key.Right or Key.Up or Key.Down or
            Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Insert or
            Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or
            Key.CapsLock or Key.NumLock or Key.Scroll or Key.PrintScreen or Key.Pause or Key.Apps or
            Key.F1 or Key.F2 or Key.F3 or Key.F4 or Key.F5 or Key.F6 or
            Key.F7 or Key.F8 or Key.F9 or Key.F10 or Key.F11 or Key.F12);
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ChatViewModel vm)
        {
            e.Handled = true;
            _ = vm.SendMessageCommand.ExecuteAsync(null);
        }
    }

    private void OnTelegramClicked(object? sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://t.me/dota2classicru") { UseShellExecute = true });

    private void OnDiscordClicked(object? sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://discord.gg/36D4WdNquT") { UseShellExecute = true });

    private void OnSiteClicked(object? sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://dotaclassic.ru/") { UseShellExecute = true });

    private void OnDotaclassicPlusClicked(object? sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://dotaclassic.ru/store") { UseShellExecute = true });

    private void OnReplyClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ChatMessageView msg } && DataContext is ChatViewModel vm)
            vm.SetReplyTarget(msg);
    }

    private void OnPickerReactClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control v &&
            v.FindAncestorOfType<FlyoutPresenter>()?.Parent is Popup popup)
            Dispatcher.UIThread.Post(() => popup.IsOpen = false);
    }

    private void OnInputEmoticonClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ChatQuickReactViewModel item } btn &&
            DataContext is ChatViewModel vm)
        {
            var caret = MessageInput.CaretIndex;
            var text = MessageInput.Text ?? "";
            vm.InputText = text[..caret] + item.Tooltip + text[caret..];
            if (btn.FindAncestorOfType<FlyoutPresenter>()?.Parent is Popup popup)
                popup.IsOpen = false;
            Dispatcher.UIThread.Post(() =>
            {
                MessageInput.CaretIndex = caret + item.Tooltip.Length;
                MessageInput.Focus();
            });
        }
    }

    private void ScrollToBottom()
    {
        var distanceFromBottom = MessagesScroll.Extent.Height - MessagesScroll.Viewport.Height - MessagesScroll.Offset.Y;
        if (distanceFromBottom <= 100)
            Dispatcher.UIThread.Post(() => MessagesScroll.ScrollToEnd(), DispatcherPriority.Loaded);
    }
}
