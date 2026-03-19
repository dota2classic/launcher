using System.Collections.Generic;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

/// <summary>Toast shown when an ACHIEVEMENT_COMPLETE notification arrives via socket.</summary>
public sealed class AchievementToastViewModel : NotificationViewModel
{
    /// <summary>
    /// Maps integer achievement key (0–29) to (i18n key name, image path).
    /// Mirrors the webapp's AchievementMapping in achievement-mapping.tsx.
    /// </summary>
    private const string AssetsBase = "avares://d2c-launcher/Assets/Images/Achievements";
    private const string SiteBase = "https://dotaclassic.ru";

    private static readonly IReadOnlyDictionary<int, (string Name, string Img)> AchievementMap =
        new Dictionary<int, (string, string)>
        {
            [0]  = ("win1hrGameAgainstTechies", "techies_1.webp"),
            [1]  = ("lastHits1000",             "fura_1.webp"),
            [2]  = ("gpm1000",                  "greed_1.webp"),
            [3]  = ("xpm1000",                  "tome_1.webp"),
            [4]  = ("allHeroChallenge",          "universal_1.webp"),
            [5]  = ("winBotGame",               "classic_1.webp"),
            [6]  = ("winSoloMidGame",           "raze_1.webp"),
            [7]  = ("gpmXpm1000",               "midas.webp"),
            [8]  = ("win1hrGame",               "wd_1.webp"),
            [9]  = ("winStreak10",              "streak_1.webp"),
            [10] = ("hardcore",                 "hardcore_2.webp"),
            [11] = ("denies50",                 "dendi_1.webp"),
            [12] = ("kills",                    "necr_1.webp"),
            [13] = ("assists",                  "assist_1.webp"),
            [14] = ("meatgrinder",              "meat_1.webp"),
            [15] = ("maxKills",                 "kills_1.webp"),
            [16] = ("maxAssists",               "zeus-1.webp"),
            [17] = ("glasscannon",              "sniper_1.webp"),
            [18] = ("lastHitsSum",              "raze_1.webp"),
            [19] = ("denySum",                  "deny_1.webp"),
            [20] = ("winUnranked",              "flag_2.webp"),
            [21] = ("towerDamage",              "tower_1.webp"),
            [22] = ("towerDamageSum",           "tower_2.webp"),
            [23] = ("heroHealing",              "heal_1.webp"),
            [24] = ("heroHealingSum",           "heal_2.webp"),
            [25] = ("allMelee",                 "melee_2.webp"),
            [26] = ("heroDamage",               "hero_dmg_1.webp"),
            [27] = ("heroDamageSum",            "hero_dmg_2.webp"),
            [28] = ("misses",                   "blind_1.webp"),
            [29] = ("deathSum",                 "leoric_1.webp"),
        };

    public string Title { get; }
    public string Description { get; }
    public string? ImageUrl { get; }
    public string AchievementsPageUrl { get; }

    public RelayCommand OpenCommand { get; }

    /// <summary>
    /// Creates an <see cref="AchievementToastViewModel"/> for the given achievement key,
    /// or returns <c>null</c> (and logs an error) if the key is not in the map.
    /// </summary>
    public static AchievementToastViewModel? TryCreate(
        string notificationId,
        string steamId,
        int achievementKey,
        IBackendApiService api,
        int cp = 0,
        int displaySeconds = 10)
    {
        if (!AchievementMap.ContainsKey(achievementKey))
        {
            AppLog.Error($"[AchievementToast] Unknown achievement key: {achievementKey}");
            return null;
        }
        return new AchievementToastViewModel(notificationId, steamId, achievementKey, api, cp, displaySeconds);
    }

    private AchievementToastViewModel(
        string notificationId,
        string steamId,
        int achievementKey,
        IBackendApiService api,
        int cp,
        int displaySeconds)
        : base(displaySeconds, notificationId: notificationId)
    {
        var info = AchievementMap[achievementKey];

        AchievementsPageUrl = $"{SiteBase}/players/{steamId}/achievements";
        Title = I18n.T($"achievement.{info.Name}.title");
        Description = I18n.T($"achievement.{info.Name}.description", ("cp", cp));
        ImageUrl = $"{AssetsBase}/{info.Img}";

        OpenCommand = new RelayCommand(() =>
        {
            Process.Start(new ProcessStartInfo(AchievementsPageUrl) { UseShellExecute = true });
            ForceClose();
        });

        // Acknowledge via REST when the toast is dismissed (naturally, manually, or via Open button).
        Closed += vm => { var _ = api.AcknowledgeNotificationAsync(notificationId); };
    }
}
