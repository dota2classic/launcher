using System;
using d2c_launcher.Util;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;

namespace d2c_launcher.Services;

public sealed class ToastNotificationService : IToastNotificationService
{
    private readonly ToastNotifierCompat _notifier;
    private const string LaunchGameArg = "d2c://game";

    public ToastNotificationService()
    {
        ToastShortcutHelper.EnsureShortcut();
        // ToastNotificationManagerCompat registers a COM activator so that activation
        // is handled in-process via ToastNotificationManagerCompat.OnActivated (wired
        // in App.axaml.cs) rather than by relaunching the exe.
        _notifier = ToastNotificationManagerCompat.CreateToastNotifier();
    }

    public void ShowMatchFound(string roomId)
    {
        try
        {
            var content = new ToastContentBuilder()
                .AddText(I18n.T("toast.matchFound.title"))
                .AddText(I18n.T("toast.matchFound.body"))
                .AddButton(new ToastButton(I18n.T("common.accept"), $"d2c://ready-check/{roomId}/accept"))
                .AddButton(new ToastButton(I18n.T("common.decline"), $"d2c://ready-check/{roomId}/decline"))
                .GetToastContent();
            content.Launch = LaunchGameArg;

            var toast = new ToastNotification(content.GetXml()) { Tag = $"room-{roomId}" };
            _notifier.Show(toast);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to show match found toast notification.", ex);
        }
    }

    public void ShowPartyInvite(string inviteId, string inviterName)
    {
        try
        {
            var content = new ToastContentBuilder()
                .AddText(I18n.T("toast.partyInvite.title"))
                .AddText(I18n.T("toast.partyInvite.body", ("name", inviterName)))
                .AddButton(new ToastButton(I18n.T("common.accept"), $"d2c://party-invite/{inviteId}/accept"))
                .AddButton(new ToastButton(I18n.T("common.decline"), $"d2c://party-invite/{inviteId}/decline"))
                .GetToastContent();
            content.Launch = LaunchGameArg;

            var toast = new ToastNotification(content.GetXml()) { Tag = $"party-invite-{inviteId}" };
            _notifier.Show(toast);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to show party invite toast notification.", ex);
        }
    }

    public void Show(string title, string body, string? tag = null, string? launchArg = null)
    {
        try
        {
            var content = new ToastContentBuilder()
                .AddText(title)
                .AddText(body)
                .GetToastContent();
            if (launchArg != null)
                content.Launch = launchArg;

            var toast = new ToastNotification(content.GetXml());
            if (tag != null)
                toast.Tag = tag;

            _notifier.Show(toast);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to show toast notification.", ex);
        }
    }

    public void ShowGoQueue(string title, string body, int modeId)
    {
        try
        {
            var content = new ToastContentBuilder()
                .AddText(title)
                .AddText(body)
                .AddButton(new ToastButton(I18n.T("toast.goQueue.enterQueueButton"), $"d2c://enter-queue/{modeId}"))
                .GetToastContent();
            content.Launch = LaunchGameArg;

            // Remove the previous go-queue notification first — Windows suppresses the
            // popup when replacing a toast with the same tag in place.
            ToastNotificationManagerCompat.History.Remove("go-queue");
            var toast = new ToastNotification(content.GetXml()) { Tag = "go-queue" };
            _notifier.Show(toast);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to show go-queue toast notification.", ex);
        }
    }
}
