using System.Collections.Generic;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

/// <summary>Toast shown when an ACHIEVEMENT_COMPLETE notification arrives via socket.</summary>
public sealed class AchievementToastViewModel : NotificationViewModel
{
    /// <summary>
    /// Maps integer achievement key (0–29) to (i18n key name, image path).
    /// Mirrors the webapp's AchievementMapping in achievement-mapping.tsx.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, (string Name, string Img)> AchievementMap =
        new Dictionary<int, (string, string)>
        {
            [0]  = ("win1hrGameAgainstTechies", "/achievement2/techies_1.webp"),
            [1]  = ("lastHits1000",             "/achievement2/fura_1.webp"),
            [2]  = ("gpm1000",                  "/achievement2/greed_1.webp"),
            [3]  = ("xpm1000",                  "/achievement2/tome_1.webp"),
            [4]  = ("allHeroChallenge",          "/achievement2/universal_1.webp"),
            [5]  = ("winBotGame",               "/achievement2/classic_1.webp"),
            [6]  = ("winSoloMidGame",           "/achievement2/raze_1.webp"),
            [7]  = ("gpmXpm1000",               "/achievement2/midas.webp"),
            [8]  = ("win1hrGame",               "/achievement2/wd_1.webp"),
            [9]  = ("winStreak10",              "/achievement2/streak_1.webp"),
            [10] = ("hardcore",                 "/achievement2/hardcore_2.webp"),
            [11] = ("denies50",                 "/achievement2/dendi_1.webp"),
            [12] = ("kills",                    "/achievement2/necr_1.webp"),
            [13] = ("assists",                  "/achievement2/assist_1.webp"),
            [14] = ("meatgrinder",              "/achievement2/meat_1.webp"),
            [15] = ("maxKills",                 "/achievement2/kills_1.webp"),
            [16] = ("maxAssists",               "/achievement2/zeus-1.webp"),
            [17] = ("glasscannon",              "/achievement2/sniper_1.webp"),
            [18] = ("lastHitsSum",              "/achievement2/raze_1.webp"),
            [19] = ("denySum",                  "/achievement2/deny_1.webp"),
            [20] = ("winUnranked",              "/achievement2/flag_2.webp"),
            [21] = ("towerDamage",              "/achievement2/tower_1.webp"),
            [22] = ("towerDamageSum",           "/achievement2/tower_2.webp"),
            [23] = ("heroHealing",              "/achievement2/heal_1.webp"),
            [24] = ("heroHealingSum",           "/achievement2/heal_2.webp"),
            [25] = ("allMelee",                 "/achievement2/melee_2.webp"),
            [26] = ("heroDamage",               "/achievement2/hero_dmg_1.webp"),
            [27] = ("heroDamageSum",            "/achievement2/hero_dmg_2.webp"),
            [28] = ("misses",                   "/achievement2/blind_1.webp"),
            [29] = ("deathSum",                 "/achievement2/leoric_1.webp"),
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
        int achievementKey,
        IBackendApiService api,
        int cp = 0,
        int displaySeconds = 10)
        : base(displaySeconds)
    {
        AchievementsPageUrl = $"{BaseUrl}/players/{steamId}/achievements";

        AchievementMap.TryGetValue(achievementKey, out var info);
        Title = I18n.T($"achievement.{info.Name}.title");
        Description = I18n.T($"achievement.{info.Name}.description", ("cp", cp));
        ImageUrl = info.Img is not null ? $"{BaseUrl}{info.Img}" : null;

        OpenCommand = new RelayCommand(() =>
        {
            Process.Start(new ProcessStartInfo(AchievementsPageUrl) { UseShellExecute = true });
            ForceClose();
        });

        // Acknowledge via REST when the toast is dismissed (naturally, manually, or via Open button).
        Closed += vm => { var _ = api.AcknowledgeNotificationAsync(notificationId); };
    }
}
