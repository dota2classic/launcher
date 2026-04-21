using System;
using d2c_launcher.Util;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;

namespace d2c_launcher.Services;

public sealed class ToastNotificationService : IToastNotificationService
{
    private readonly ToastNotifierCompat _notifier;

    public ToastNotificationService()
    {
        ToastShortcutHelper.EnsureShortcut();
        // ToastNotificationManagerCompat registers a COM activator so that activation
        // is handled in-process via ToastNotificationManagerCompat.OnActivated (wired
        // in App.axaml.cs) rather than by relaunching the exe.
        _notifier = ToastNotificationManagerCompat.CreateToastNotifier();
    }

    public void ShowMatchFound()
    {
        Show(I18n.T("toast.matchFound.title"), I18n.T("toast.matchFound.body"));
    }

    public void ShowPartyInvite(string inviterName)
    {
        Show(
            I18n.T("toast.partyInvite.title"),
            I18n.T("toast.partyInvite.body", ("name", inviterName)));
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
            content.Launch = "d2c://game";

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
