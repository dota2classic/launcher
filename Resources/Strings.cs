using d2c_launcher.Services;

namespace d2c_launcher.Resources;

/// <summary>
/// Centralized Russian UI strings for D2C Launcher.
/// All values are backed by <see cref="I18n"/> / <c>Resources/Locales/ru.json</c>.
/// XAML call sites using <c>{x:Static res:Strings.X}</c> require no changes.
/// </summary>
public static class Strings
{
    // ── Settings section headers ───────────────────────────────────────────────
    public static string SectionAttack => I18n.T("settings.sectionAttack");
    public static string SectionCamera => I18n.T("settings.sectionCamera");

    // ── Settings tabs ──────────────────────────────────────────────────────────
    public static string TabGameplay => I18n.T("settings.tabGameplay");
    public static string TabLauncher => I18n.T("settings.tabLauncher");
    public static string TabGame     => I18n.T("settings.tabGame");
    public static string TabGeneral  => I18n.T("settings.tabGeneral");
    public static string TabMatches  => I18n.T("settings.tabMatches");
    public static string TabChat     => I18n.T("settings.tabChat");

    // ── Settings: General / Launcher ───────────────────────────────────────────
    public static string GameDirectory          => I18n.T("settings.gameDirectory");
    public static string Change                 => I18n.T("settings.change");
    public static string CloseToTray            => I18n.T("settings.closeToTray");
    public static string CloseToTrayDescription => I18n.T("settings.closeToTrayDescription");
    public static string AutoConnectToServer    => I18n.T("settings.autoConnectToServer");
    public static string AutoConnectDescription => I18n.T("settings.autoConnectDescription");
    public static string DefenderExclusionTitle => I18n.T("settings.defenderExclusionTitle");
    public static string DefenderExclusionAdded => I18n.T("settings.defenderExclusionAdded");
    public static string LaunchParameters       => I18n.T("settings.launchParameters");
    public static string ExtraArgsHint          => I18n.T("settings.extraArgsHint");
    public static string UiScale                => I18n.T("settings.uiScale");
    public static string UiScaleDescription     => I18n.T("settings.uiScaleDescription");
    public static string SendDebugInfo          => I18n.T("settings.sendDebugInfo");
    public static string DebugInfoSent          => I18n.T("settings.debugInfoSent");

    // ── Settings: Visuals ─────────────────────────────────────────────────────
    public static string GameLanguage                => I18n.T("settings.gameLanguage");
    public static string GameLanguageDescription     => I18n.T("settings.gameLanguageDescription");
    public static string SkipIntro                   => I18n.T("settings.skipIntro");
    public static string SkipIntroDescription        => I18n.T("settings.skipIntroDescription");
    public static string FullscreenMode              => I18n.T("settings.fullscreenMode");
    public static string LaunchFullscreenDescription => I18n.T("settings.launchFullscreenDescription");
    public static string BorderlessMode              => I18n.T("settings.borderlessMode");
    public static string LaunchBorderlessDescription => I18n.T("settings.launchBorderlessDescription");
    public static string Resolution                  => I18n.T("settings.resolution");
    public static string ScreenResolutionGame        => I18n.T("settings.screenResolutionGame");
    public static string Monitor                     => I18n.T("settings.monitor");

    // ── Settings: Gameplay ────────────────────────────────────────────────────
    public static string AutoAttack                  => I18n.T("settings.autoAttack");
    public static string AutoAttackMode              => I18n.T("settings.autoAttackMode");
    public static string RightClickAttack            => I18n.T("settings.rightClickAttack");
    public static string RightClickAttackDescription => I18n.T("settings.rightClickAttackDescription");
    public static string DisableScrollZoom           => I18n.T("settings.disableScrollZoom");
    public static string DisableScrollZoomHint       => I18n.T("settings.disableScrollZoomHint");
    public static string AutoRepeatRMB               => I18n.T("settings.autoRepeatRMB");
    public static string RmbHoldDescription          => I18n.T("settings.rmbHoldDescription");
    public static string TeleportRequiresStop        => I18n.T("settings.teleportRequiresStop");
    public static string TeleportCancelHint          => I18n.T("settings.teleportCancelHint");
    public static string CameraDistance              => I18n.T("settings.cameraDistance");
    public static string CameraDistanceHint          => I18n.T("settings.cameraDistanceHint");
    public static string CameraOnSpawn               => I18n.T("settings.cameraOnSpawn");
    public static string CameraOnSpawnDescription    => I18n.T("settings.cameraOnSpawnDescription");

    // ── Settings: DLC ────────────────────────────────────────────────────────
    public static string ColDlc            => I18n.T("settings.colDlc");
    public static string AdditionalContent => I18n.T("settings.additionalContent");
    public static string Apply             => I18n.T("settings.apply");

    // ── Windows Defender modal ────────────────────────────────────────────────
    public static string DefenderAddExclusionPrompt => I18n.T("defender.addExclusionPrompt");
    public static string DefenderExclusionExplanation => I18n.T("defender.exclusionExplanation");
    public static string UacNote      => I18n.T("defender.uacNote");
    public static string Skip         => I18n.T("common.skip");
    public static string AddException => I18n.T("defender.addException");

    // ── Game search / matchmaking ─────────────────────────────────────────────
    public static string SearchGame    => I18n.T("matchmaking.searchGame");
    public static string Play          => I18n.T("matchmaking.play");
    public static string Stop          => I18n.T("matchmaking.stop");
    public static string Connect       => I18n.T("matchmaking.connect");
    public static string CancelSearch  => I18n.T("matchmaking.cancelSearch");
    public static string SearchingGame => I18n.T("matchmaking.searchingGame");

    // ── Party ─────────────────────────────────────────────────────────────────
    public static string GroupLabel          => I18n.T("party.groupLabel");
    public static string LeaveParty          => I18n.T("party.leaveParty");
    public static string SelectPlayer        => I18n.T("party.selectPlayer");
    public static string PlayerNickname      => I18n.T("party.playerNickname");
    public static string InvitesToParty      => I18n.T("party.invitesToParty");
    public static string InvitedToYourParty  => I18n.T("party.invitedToYourParty");

    // ── Ready check ──────────────────────────────────────────────────────────
    public static string GameFound          => I18n.T("room.gameFound");
    public static string SearchingGameServer => I18n.T("room.searchingGameServer");
    public static string Accept             => I18n.T("common.accept");
    public static string AcceptUpper        => I18n.T("common.acceptUpper");
    public static string Decline            => I18n.T("common.decline");
    public static string DeclineUpper       => I18n.T("common.declineUpper");
    public static string WaitingForPlayers  => I18n.T("room.waitingForPlayers");

    // ── Notifications / toasts ───────────────────────────────────────────────
    public static string VerifyIntegrity    => I18n.T("notifications.verifyIntegrity");
    public static string AchievementUnlocked => I18n.T("notifications.achievementComplete");
    public static string OpenAchievements   => I18n.T("notifications.openAchievements");

    // ── Profile ──────────────────────────────────────────────────────────────
    public static string LoadingProfile => I18n.T("profile.loading");
    public static string Back           => I18n.T("common.back");
    public static string MatchesSuffix  => I18n.T("profile.matchesSuffix");
    public static string WinRate        => I18n.T("profile.winRate");
    public static string WinRateLabel   => I18n.T("profile.winRateLabel");
    public static string ColRating      => I18n.T("profile.colRating");
    public static string ColRank        => I18n.T("profile.colRank");
    public static string ColHero        => I18n.T("profile.colHero");
    public static string BestHeroes     => I18n.T("profile.bestHeroes");
    public static string SeasonStats    => I18n.T("profile.seasonStats");
    public static string ColAssists     => I18n.T("profile.colAssists");
    public static string ShowAll        => I18n.T("profile.showAll");
    public static string Kills          => I18n.T("profile.kills");
    public static string Deaths         => I18n.T("profile.deaths");
    public static string Assists        => I18n.T("profile.assists");
    public static string GamesPlayed    => I18n.T("profile.gamesPlayed");
    public static string AbandonedGames => I18n.T("profile.abandonedGames");
    public static string TimeInGame     => I18n.T("profile.timeInGame");

    // ── Live tab ──────────────────────────────────────────────────────────────
    public static string Broadcasts      => I18n.T("live.broadcasts");
    public static string LoadingEllipsis => I18n.T("common.loadingEllipsis");
    public static string NoActiveGames   => I18n.T("live.noActiveGames");
    public static string GameMode        => I18n.T("live.gameMode");
    public static string Watch           => I18n.T("live.watch");
    public static string ColScore        => I18n.T("live.colScore");
    public static string ColTime         => I18n.T("live.colTime");

    // ── Chat ─────────────────────────────────────────────────────────────────
    public static string EnterMessage => I18n.T("chat.enterMessage");
    public static string Loading      => I18n.T("common.loading");
    public static string Website      => I18n.T("common.website");

    // ── Download / update ────────────────────────────────────────────────────
    public static string Retry                  => I18n.T("common.retry");
    public static string DownloadGame           => I18n.T("download.downloadGame");
    public static string AlreadyHaveDota        => I18n.T("download.alreadyHaveDota");
    public static string SelectGameFolder       => I18n.T("download.selectGameFolder");
    public static string Or                     => I18n.T("common.or");
    public static string SelectContentToInstall => I18n.T("download.selectContentToInstall");
    public static string RequiredPackagesNote   => I18n.T("download.requiredPackagesNote");
    public static string UpdateAvailable        => I18n.T("download.updateAvailable");
    public static string RestartAndUpdate       => I18n.T("download.restartAndUpdate");

    // ── LaunchSteamFirst ─────────────────────────────────────────────────────
    public static string SteamRequiredText   => I18n.T("steam.requiredText");
    public static string SteamNotRunningHint => I18n.T("steam.notRunningHint");
    public static string OpenSteam           => I18n.T("steam.openSteam");
    public static string TryAgain            => I18n.T("common.tryAgain");

    // ── Settings modal ───────────────────────────────────────────────────────
    public static string Settings => I18n.T("settings.title");

    // ── Main launcher ─────────────────────────────────────────────────────────
    public static string AbandonGameTitle   => I18n.T("main.abandonGameTitle");
    public static string AbandonConfirmText => I18n.T("main.abandonConfirmText");
    public static string Leave              => I18n.T("common.leave");
    public static string Cancel             => I18n.T("common.cancel");
    public static string WelcomeTitle       => I18n.T("main.welcomeTitle");
    public static string StepFormat         => I18n.T("main.stepFormat");

    // ── CS-side strings ──────────────────────────────────────────────────────
    public static string AccountPermabanned        => I18n.T("game.accountPermabanned");
    public static string InQueue                   => I18n.T("game.inQueue");
    public static string NotInQueue                => I18n.T("game.notInQueue");
    public static string Always                    => I18n.T("common.always");
    public static string Disabled                  => I18n.T("common.disabled");
    public static string AfterSpell                => I18n.T("game.afterSpell");
    public static string SelectDotaExe             => I18n.T("game.selectDotaExe");
    public static string SelectAtLeastOneMode      => I18n.T("game.selectAtLeastOneMode");
    public static string RestrictedModesUnselected => I18n.T("game.restrictedModesUnselected");
    public static string GoQueueTitle              => I18n.T("game.goQueueTitle");
    public static string GoQueueContent            => I18n.T("game.goQueueContent");
    public static string HeroPicking               => I18n.T("game.heroPicking");
    public static string FolderNotDotaclassic      => I18n.T("game.folderNotDotaclassic");
    public static string WrongPatchVersionFormat   => I18n.T("game.wrongPatchVersionFormat");
    public static string Done                      => I18n.T("common.done");
    public static string Next                      => I18n.T("common.next");
    public static string NeedOneWinForAccess       => I18n.T("game.needOneWinForAccess");
    public static string LoadingPlayers            => I18n.T("game.loadingPlayers");
    public static string Launch                    => I18n.T("game.launch");
    public static string GameFinished              => I18n.T("game.gameFinished");
    public static string GameInProgress            => I18n.T("game.gameInProgress");
    public static string GameUpdated               => I18n.T("game.gameUpdated");
    public static string Initializing              => I18n.T("game.initializing");
    public static string Lobby                     => I18n.T("game.lobby");
    public static string GameStarting              => I18n.T("game.gameStarting");
    public static string StartPlaying              => I18n.T("common.startPlaying");
    public static string NotSpecified              => I18n.T("common.notSpecified");
    public static string InvalidGameFolder         => I18n.T("game.invalidGameFolder");
    public static string NoFolderAccess            => I18n.T("game.noFolderAccess");
    public static string NoFolderAccessTitle       => I18n.T("game.noFolderAccessTitle");
    public static string NoModeAccess              => I18n.T("game.noModeAccess");
    public static string No                        => I18n.T("common.no");
    public static string ConnectingToServer        => I18n.T("game.connectingToServer");
    public static string ConnectingToSteam         => I18n.T("game.connectingToSteam");
    public static string VsBots                    => I18n.T("game.vsBots");
    public static string VerifyingFiles            => I18n.T("game.verifyingFiles");
    public static string Mode5v5                   => I18n.T("game.mode5v5");
    public static string Ranked5v5                 => I18n.T("game.ranked5v5");
    public static string NeedBotsGameForMode       => I18n.T("game.needBotsGameForMode");
    public static string Turbo                     => I18n.T("game.turbo");
    public static string Tournament1v1             => I18n.T("game.tournament1v1");
    public static string Tournament                => I18n.T("game.tournament");
    public static string Bot                       => I18n.T("game.bot");
    public static string DeletingDlcFiles          => I18n.T("game.deletingDlcFiles");
    public static string InstallingComponents      => I18n.T("game.installingComponents");
    public static string GameFilesCorrupted        => I18n.T("game.gameFilesCorrupted");
    public static string FailedToLoadPackages      => I18n.T("game.failedToLoadPackages");
    public static string LoadingError              => I18n.T("common.loadingError");
    public static string StopLabel                 => I18n.T("common.stopLabel");

    // ── Month abbreviations ────────────────────────────────────────────────────
    public static string MonthJan => I18n.T("month.jan");
    public static string MonthFeb => I18n.T("month.feb");
    public static string MonthMar => I18n.T("month.mar");
    public static string MonthApr => I18n.T("month.apr");
    public static string MonthMay => I18n.T("month.may");
    public static string MonthJun => I18n.T("month.jun");
    public static string MonthJul => I18n.T("month.jul");
    public static string MonthAug => I18n.T("month.aug");
    public static string MonthSep => I18n.T("month.sep");
    public static string MonthOct => I18n.T("month.oct");
    public static string MonthNov => I18n.T("month.nov");
    public static string MonthDec => I18n.T("month.dec");
}
