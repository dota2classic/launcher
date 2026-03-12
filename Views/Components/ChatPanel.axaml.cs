using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using d2c_launcher.Models;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Views.Components;

public partial class ChatPanel : UserControl
{
    public ChatPanel()
    {
        InitializeComponent();
        AddHandler(RichMessageBlock.PlayerLinkClickedEvent, OnPlayerLinkClicked);
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

    private void OnTelegramClicked(object? sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://t.me/dota2classicru") { UseShellExecute = true });

    private void OnDiscordClicked(object? sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://discord.gg/36D4WdNquT") { UseShellExecute = true });

    private void OnSiteClicked(object? sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://dotaclassic.ru/") { UseShellExecute = true });

    private void ScrollToBottom()
    {
        var distanceFromBottom = MessagesScroll.Extent.Height - MessagesScroll.Viewport.Height - MessagesScroll.Offset.Y;
        if (distanceFromBottom <= 100)
            Dispatcher.UIThread.Post(() => MessagesScroll.ScrollToEnd(), DispatcherPriority.Loaded);
    }
}
