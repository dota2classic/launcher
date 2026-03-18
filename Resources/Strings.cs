namespace d2c_launcher.Resources;

/// <summary>
/// Centralized Russian UI strings for D2C Launcher.
/// </summary>
public static class Strings
{
    // ── Settings tabs ──────────────────────────────────────────────────────────
    public static string TabVisuals => "ВИЗУАЛЬНЫЕ";
    public static string TabGameplay => "ГЕЙМПЛЕЙ";
    public static string TabLauncher => "ЛАУНЧЕР";
    public static string TabGame => "ИГРА";
    public static string TabGeneral => "ОБЩЕЕ";
    public static string TabMatches => "МАТЧИ";
    public static string TabChat => "ЧАТ";

    // ── Settings: General / Launcher ───────────────────────────────────────────
    public static string GameDirectory => "ДИРЕКТОРИЯ ИГРЫ";
    public static string Change => "Изменить";
    public static string AutoUpdateLauncher => "Автообновление лаунчера";
    public static string InstallUpdatesOnStartup => "Устанавливать обновления при запуске";
    public static string CloseToTray => "Сворачивать в трей при закрытии";
    public static string CloseToTrayDescription => "Закрытие окна скрывает лаунчер в трей";
    public static string AutoConnectToServer => "Автоподключение к серверу";
    public static string AutoConnectDescription => "Автоматически подключаться к игре при нахождении сервера";
    public static string DefenderExclusionTitle => "Исключение Windows Defender";
    public static string DefenderExclusionAdded => "Папка с игрой добавлена в исключения Defender";
    public static string LaunchParameters => "Параметры запуска";
    public static string ExtraArgsHint => "Добавляются перед стандартными аргументами лаунчера";
    public static string UiScale => "Масштаб интерфейса";
    public static string UiScaleDescription => "Увеличивает размер шрифта во всём лаунчере";
    public static string SendDebugInfo => "Отправить отладочную информацию";
    public static string DebugInfoSent => "Отладочная информация отправлена";

    // ── Settings: Visuals ─────────────────────────────────────────────────────
    public static string GameLanguage => "Язык игры";
    public static string GameLanguageDescription => "Язык интерфейса и озвучки";
    public static string SkipIntro => "Пропускать интро";
    public static string SkipIntroDescription => "Пропускать заставку при запуске";
    public static string FullscreenMode => "Полноэкранный режим";
    public static string LaunchFullscreenDescription => "Запускать игру в полном экране";
    public static string BorderlessMode => "Безрамочный режим";
    public static string LaunchBorderlessDescription => "Запускать игру без рамки окна";
    public static string Resolution => "Разрешение";
    public static string ScreenResolutionGame => "Разрешение экрана игры";
    public static string Monitor => "монитор";

    // ── Settings: Gameplay ────────────────────────────────────────────────────
    public static string AutoAttack => "Автоатака";
    public static string AutoAttackMode => "Режим автоматической атаки";
    public static string RightClickAttack => "Атака правой кнопкой";
    public static string RightClickAttackDescription => "Атаковать врагов правой кнопкой мыши";
    public static string DisableScrollZoom => "Отключить приближение колесиком";
    public static string DisableScrollZoomHint => "Нельзя будет изменять масштаб камеры колесом мыши";
    public static string AutoRepeatRMB => "Автоповтор правой кнопки";
    public static string RmbHoldDescription => "Удерживание ПКМ повторяет команду";
    public static string TeleportRequiresStop => "Телепорт требует остановки";
    public static string TeleportCancelHint => "Для отмены телепорта нужно нажать «Отменить»";
    public static string CameraDistance => "Дальность камеры";
    public static string CameraDistanceHint => "По умолчанию 1134. Диапазон: 1000–1600.";
    public static string CameraOnSpawn => "Камера на героя при спавне";
    public static string CameraOnSpawnDescription => "Переводить камеру к герою при возрождении";

    // ── Settings: DLC ────────────────────────────────────────────────────────
    public static string ColDlc => "ДЛС";
    public static string AdditionalContent => "ДОПОЛНИТЕЛЬНЫЙ КОНТЕНТ";
    public static string Apply => "Применить";

    // ── Windows Defender modal ────────────────────────────────────────────────
    public static string DefenderAddExclusionPrompt => "Добавить папку с игрой в исключения Windows Defender?";
    public static string DefenderExclusionExplanation => "Без исключения антивирус проверяет каждый файл при скачивании и проверке игры, что замедляет процесс и нагружает диск и процессор.";
    public static string UacNote => "Потребуется подтверждение администратора (UAC).";
    public static string Skip => "Пропустить";
    public static string AddException => "Добавить исключение";

    // ── Game search / matchmaking ─────────────────────────────────────────────
    public static string SearchGame => "ПОИСК ИГРЫ";
    public static string Play => "ИГРАТЬ";
    public static string Stop => "СТОП";
    public static string Connect => "ПОДКЛЮЧИТЬСЯ";
    public static string CancelSearch => "ОТМЕНИТЬ ПОИСК";
    public static string SearchingGame => "Поиск игры";

    // ── Party ─────────────────────────────────────────────────────────────────
    public static string GroupLabel => "ГРУППА";
    public static string LeaveParty => "Покинуть группу";
    public static string SelectPlayer => "ВЫБРАТЬ ИГРОКА";
    public static string PlayerNickname => "Никнейм игрока";
    public static string InvitesToParty => "Приглашает в группу";
    public static string InvitedToYourParty => "приглашён в вашу группу";

    // ── Ready check ──────────────────────────────────────────────────────────
    public static string GameFound => "Игра найдена!";
    public static string SearchingGameServer => "Идёт поиск игрового сервера...";
    public static string Accept => "Принять";
    public static string AcceptUpper => "ПРИНЯТЬ";
    public static string Decline => "Отклонить";
    public static string DeclineUpper => "ОТКЛОНИТЬ";
    public static string WaitingForPlayers => "Ожидание других игроков...";

    // ── Notifications / toasts ───────────────────────────────────────────────
    public static string VerifyIntegrity => "ПРОВЕРИТЬ ЦЕЛОСТНОСТЬ";

    // ── Profile ──────────────────────────────────────────────────────────────
    public static string LoadingProfile => "Загрузка профиля...";
    public static string Back => "Назад";
    public static string MatchesSuffix => "матчей";
    public static string WinRate => "ДОЛЯ ПОБЕД";
    public static string WinRateLabel => "Доля побед";
    public static string ColRating => "РЕЙТИНГ";
    public static string ColRank => "РАНГ";
    public static string ColHero => "ГЕРОЙ";
    public static string BestHeroes => "ЛУЧШИЕ ГЕРОИ";
    public static string SeasonStats => "СТАТИСТИКА ЗА СЕЗОН";
    public static string ColAssists => "ОТЗЫВЫ";
    public static string ShowAll => "Показать все →";
    public static string Kills => "Убийств";
    public static string Deaths => "Смертей";
    public static string Assists => "Помощи";
    public static string GamesPlayed => "Игр сыграно";
    public static string AbandonedGames => "Покинутых игр";
    public static string TimeInGame => "Времени в игре";

    // ── Live tab ──────────────────────────────────────────────────────────────
    public static string Broadcasts => "ТРАНСЛЯЦИИ";
    public static string LoadingEllipsis => "Загрузка…";
    public static string NoActiveGames => "Нет активных игр";
    public static string GameMode => "РЕЖИМ ИГРЫ";
    public static string Watch => "СМОТРЕТЬ";
    public static string ColScore => "СЧЁТ";
    public static string ColTime => "ВРЕМЯ";

    // ── Chat ─────────────────────────────────────────────────────────────────
    public static string EnterMessage => "Введите сообщение";
    public static string Loading => "Загрузка...";
    public static string Website => "Сайт";

    // ── Download / update ────────────────────────────────────────────────────
    public static string Retry => "Повторить";
    public static string DownloadGame => "Скачать игру";
    public static string AlreadyHaveDota => "У меня уже установлена Дота";
    public static string SelectGameFolder => "Выберите папку для установки игры";
    public static string Or => "ИЛИ";
    public static string SelectContentToInstall => "Выберите контент для установки";
    public static string RequiredPackagesNote => "Обязательные пакеты отмечены и будут установлены автоматически.";
    public static string UpdateAvailable => "Доступна новая версия, готова к установке.";
    public static string RestartAndUpdate => "Перезапустить и обновить";

    // ── LaunchSteamFirst ─────────────────────────────────────────────────────
    public static string SteamRequiredText => "Для запуска лаунчера необходим\nзапущенный и авторизованный ";
    public static string SteamNotRunningHint => "Убедитесь, что Steam запущен и вы вошли в аккаунт. Окно обновится автоматически.";
    public static string OpenSteam => "Открыть Steam";
    public static string TryAgain => "Попробовать снова";

    // ── Settings modal ───────────────────────────────────────────────────────
    public static string Settings => "НАСТРОЙКИ";

    // ── Main launcher ─────────────────────────────────────────────────────────
    public static string AbandonGameTitle => "Покинуть игру?";
    public static string AbandonConfirmText => "Вы точно хотите покинуть игру?\nВ режимах 5х5 это может привести к временному запрету на поиск игры.";
    public static string Leave => "Покинуть";
    public static string Cancel => "Отмена";
    public static string WelcomeTitle => "Добро пожаловать в D2C Launcher";
    public static string StepFormat => "Шаг {0} из {1}";

    // ── CS-side strings ──────────────────────────────────────────────────────
    public static string AccountPermabanned => "Аккаунт заблокирован навсегда";
    public static string InQueue => "В поиске";
    public static string NotInQueue => "Не в поиске";
    public static string Always => "Всегда";
    public static string Disabled => "Выключена";
    public static string AfterSpell => "После заклинания";
    public static string SelectDotaExe => "Выберите dota.exe";
    public static string SelectAtLeastOneMode => "Выберите хотя бы один режим игры для поиска";
    public static string GoQueueTitle => "Давай поиграем в {0}";
    public static string GoQueueContent => "В поиске уже {0} игроков!";
    public static string HeroPicking => "Выбор героев";
    public static string FolderNotDotaclassic => "Выбранная папка не является Dotaclassic.";
    public static string WrongPatchVersionFormat => "Выбранная папка содержит другой патч Dota 2 (версия {0}). Dotaclassic использует патч 6.84. Выберите правильную папку или скачайте игру заново.";
    public static string Done => "Готово!";
    public static string Next => "Далее";
    public static string NeedOneWinForAccess => "Для доступа выиграйте хотя бы одну игру";
    public static string LoadingPlayers => "Загрузка игроков";
    public static string Launch => "Запустить";
    public static string GameFinished => "Игра завершена";
    public static string GameInProgress => "Игра идет";
    public static string GameUpdated => "Игра обновлена";
    public static string Initializing => "Инициализация";
    public static string Lobby => "Лобби";
    public static string GameStarting => "Начало игры";
    public static string StartPlaying => "Начать играть";
    public static string NotSpecified => "Не указано";
    public static string InvalidGameFolder => "Неверная папка с игрой";
    public static string NoFolderAccess => "Нет доступа к выбранной папке. Выберите другую папку для установки.";
    public static string NoFolderAccessTitle => "Нет доступа к папке";
    public static string NoModeAccess => "Нет доступа к режиму";
    public static string No => "Нет";
    public static string ConnectingToServer => "Подключение к серверу...";
    public static string ConnectingToSteam => "Подключение к Steam...";
    public static string VsBots => "Против ботов";
    public static string VerifyingFiles => "Проверка файлов...";
    public static string Mode5v5 => "Обычная 5x5";
    public static string Ranked5v5 => "Рейтинговая 5x5";
    public static string NeedBotsGameForMode => "Сыграйте против ботов для открытия режима";
    public static string Turbo => "Турбо";
    public static string Tournament1v1 => "Турнир 1x1";
    public static string Tournament => "Турнир";
    public static string Bot => "Бот";
    public static string DeletingDlcFiles => "Удаление файлов DLC...";
    public static string InstallingComponents => "Установка компонентов...";
    public static string GameFilesCorrupted => "Файлы игры повреждены или удалены антивирусом.";
    public static string FailedToLoadPackages => "Не удалось загрузить список пакетов с сервера.";
    public static string LoadingError => "Ошибка загрузки";
    public static string StopLabel => "Остановить";

    // ── Month abbreviations ────────────────────────────────────────────────────
    public static string MonthJan => "янв.";
    public static string MonthFeb => "фев.";
    public static string MonthMar => "мар.";
    public static string MonthApr => "апр.";
    public static string MonthMay => "мая";
    public static string MonthJun => "июн.";
    public static string MonthJul => "июл.";
    public static string MonthAug => "авг.";
    public static string MonthSep => "сен.";
    public static string MonthOct => "окт.";
    public static string MonthNov => "ноя.";
    public static string MonthDec => "дек.";
}
