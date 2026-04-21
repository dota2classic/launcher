namespace d2c_launcher.Services;

public interface IToastNotificationService
{
    void ShowMatchFound(string roomId);
    void ShowPartyInvite(string inviteId, string inviterName);
    void Show(string title, string body, string? tag = null, string? launchArg = null);
    void ShowGoQueue(string title, string body, int modeId);
}
