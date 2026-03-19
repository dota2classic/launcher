using System.Collections.Generic;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

/// <summary>Toast shown when an ACHIEVEMENT_COMPLETE notification arrives via socket.</summary>
public sealed class AchievementToastViewModel : NotificationViewModel
{
    /// <summary>Maps integer achievement key (0-29) to image path relative to dotaclassic.ru.</summary>
    private static readonly IReadOnlyDictionary<int, string> AchievementImageMap =
        new Dictionary<int, string>
        {
            [0]  = "/achievement2/techies_1.webp",
            [1]  = "/achievement2/fura_1.webp",
            [2]  = "/achievement2/greed_1.webp",
            [3]  = "/achievement2/tome_1.webp",
            [4]  = "/achievement2/universal_1.webp",
            [5]  = "/achievement2/classic_1.webp",
            [6]  = "/achievement2/raze_1.webp",
            [7]  = "/achievement2/midas.webp",
            [8]  = "/achievement2/wd_1.webp",
            [9]  = "/achievement2/streak_1.webp",
            [10] = "/achievement2/hardcore_2.webp",
            [11] = "/achievement2/dendi_1.webp",
            [12] = "/achievement2/necr_1.webp",
            [13] = "/achievement2/assist_1.webp",
            [14] = "/achievement2/meat_1.webp",
            [15] = "/achievement2/kills_1.webp",
            [16] = "/achievement2/zeus-1.webp",
            [17] = "/achievement2/sniper_1.webp",
            [18] = "/achievement2/raze_1.webp",
            [19] = "/achievement2/deny_1.webp",
            [20] = "/achievement2/flag_2.webp",
            [21] = "/achievement2/tower_1.webp",
            [22] = "/achievement2/tower_2.webp",
            [23] = "/achievement2/heal_1.webp",
            [24] = "/achievement2/heal_2.webp",
            [25] = "/achievement2/melee_2.webp",
            [26] = "/achievement2/hero_dmg_1.webp",
            [27] = "/achievement2/hero_dmg_2.webp",
            [28] = "/achievement2/blind_1.webp",
            [29] = "/achievement2/leoric_1.webp",
        };

    private const string BaseUrl = "https://dotaclassic.ru";

    public string Title { get; }
    public string Description { get; }
    public string? ImageUrl { get; }
    public string AchievementsPageUrl { get; }

    public RelayCommand OpenCommand { get; }

    public AchievementToastViewModel(
        string notificationId,
        string steamId,
        string title,
        string description,
        int achievementKey,
        IBackendApiService api,
        int displaySeconds = 10)
        : base(displaySeconds)
    {
        Title = title;
        Description = description;
        AchievementsPageUrl = $"{BaseUrl}/players/{steamId}/achievements";

        ImageUrl = AchievementImageMap.TryGetValue(achievementKey, out var path)
            ? $"{BaseUrl}{path}"
            : null;

        OpenCommand = new RelayCommand(() =>
        {
            Process.Start(new ProcessStartInfo(AchievementsPageUrl) { UseShellExecute = true });
            ForceClose();
        });

        // Acknowledge via REST when the toast is dismissed (naturally, manually, or via Open button).
        Closed += vm => { var _ = api.AcknowledgeNotificationAsync(notificationId); };
    }
}
