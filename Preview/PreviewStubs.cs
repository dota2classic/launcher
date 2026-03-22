using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Api;
using d2c_launcher.Integration;
using d2c_launcher.Models;
using d2c_launcher.Services;

namespace d2c_launcher.Preview;

internal sealed class StubSteamManager : ISteamManager
{
    public User? CurrentUser => null;
    public SteamStatus SteamStatus => SteamStatus.NotRunning;
    public string? CurrentAuthTicket => null;
    public int BridgeFailStreak => 0;
    public string? LastBridgeStatus => null;
#pragma warning disable CS0067
    public event Action<User?>? OnUserUpdated;
    public event Action<SteamStatus>? OnSteamStatusUpdated;
    public event Action<string?>? OnSteamAuthorizationChanged;
    public event Action? OnSteamPolled;
#pragma warning restore CS0067
    public void PollSteamState() { }
    public void ResetBridgeFailStreak() { }
    public void Dispose() { }
}

internal sealed class StubQueueSocketService : IQueueSocketService
{
    public GameCoordinatorState State => GameCoordinatorState.Disconnected;
#pragma warning disable CS0067
    public event Action<GameCoordinatorState>? StateChanged;
    public event Action<PartyDto>? PartyUpdated;
    public event Action<PlayerQueueStateMessage>? PlayerQueueStateUpdated;
    public event Action<PlayerRoomStateMessage?>? PlayerRoomStateUpdated;
    public event Action<PlayerRoomStateMessage?>? PlayerRoomFound;
    public event Action<PlayerGameStateMessage?>? PlayerGameStateUpdated;
    public event Action<QueueStateMessage>? QueueStateUpdated;
    public event Action<PlayerServerSearchingMessage>? ServerSearchingUpdated;
    public event Action<OnlineUpdateMessage>? OnlineUpdated;
    public event Action<PartyInviteReceivedMessage>? PartyInviteReceived;
    public event Action<PartyInviteExpiredMessage>? PartyInviteExpired;
    public event Action<PlayerPartyInvitationsMessage>? PartyInvitationsUpdated;
    public event Action<NotificationCreatedMessage>? NotificationCreated;
    public event Action<PleaseEnterQueueMessage>? PleaseEnterQueue;
#pragma warning restore CS0067

    public Task ConnectAsync(string token, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task EnterQueueAsync(MatchmakingMode[] modes, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LeaveAllQueuesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetReadyCheckAsync(string roomId, bool accept, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task InviteToPartyAsync(string invitedPlayerId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AcceptPartyInviteAsync(string inviteId, bool accept, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LeavePartyAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Dispose() { }
}

internal sealed class StubBackendApiService : IBackendApiService
{
    public void SetBearerToken(string? token) { }

    public Task<PartySnapshot> GetMyPartySnapshotAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PartySnapshot([
            new PartyMemberView("76561198000000001", "Player One", null),
            new PartyMemberView("76561198000000002", "Player Two", null),
        ], null));

    public PartySnapshot MapPartyDto(d2c_launcher.Api.PartyDto party)
        => new PartySnapshot(Array.Empty<PartyMemberView>(), null);

    public Task<IReadOnlyList<MatchmakingModeInfo>> GetEnabledMatchmakingModesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MatchmakingModeInfo>>([
            new MatchmakingModeInfo(8,  "Highroom 5x5"),
            new MatchmakingModeInfo(1,  "Обычная 5x5"),
            new MatchmakingModeInfo(13, "Турбо"),
            new MatchmakingModeInfo(2,  "1x1 Мид"),
            new MatchmakingModeInfo(7,  "Против ботов"),
        ]);

    public Task<IReadOnlyList<InviteCandidateView>> SearchPlayersAsync(string name, int count = 25, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<InviteCandidateView>>([]);

    public Task<(string? Name, string? AvatarUrl)?> GetUserInfoAsync(string steamId, CancellationToken cancellationToken = default)
        => Task.FromResult<(string?, string?)?>(null);

    public Task<(int InGame, int OnSite)> GetOnlineStatsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult((0, 0));

    public Task<IReadOnlyList<ChatMessageData>> GetChatMessagesAsync(string threadId, int limit, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ChatMessageData>>([
            new ChatMessageData("1", threadId, "Давай сыграем!", "2025-03-05T20:00:00Z", "111", "MaxiKo", null, false),
            new ChatMessageData("2", threadId, "трамвай потеет)", "2025-03-05T20:00:30Z", "111", "MaxiKo", null, false),
            new ChatMessageData("3", threadId, "Когда сервер поднимут?", "2025-03-05T20:05:00Z", "222", "лоутаб секьюрити", null, false),
            new ChatMessageData("4", threadId, "если он есть", "2025-03-05T20:05:20Z", "222", "лоутаб секьюрити", null, false),
            new ChatMessageData("5", threadId, "геге, мормышка победил", "2025-03-05T20:10:00Z", "111", "MaxiKo", null, false),
            new ChatMessageData("6", threadId, "играю сносно https://dotaclassic.ru/players/198768255 але", "2025-03-05T20:11:00Z", "222", "лоутаб секьюрити", null, false),
        ]);

    public Task PostChatMessageAsync(string threadId, string content, string? replyMessageId = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<EmoticonData>> GetEmoticonsAsync(string? steamId = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<EmoticonData>>([]);

    public Task<Models.LiveMatchInfo?> GetLiveMatchAsync(int matchId, CancellationToken cancellationToken = default)
        => Task.FromResult<Models.LiveMatchInfo?>(null);

    public Task<Models.PlayerProfileData?> GetPlayerSummaryAsync(string steamId, CancellationToken cancellationToken = default)
        => Task.FromResult<Models.PlayerProfileData?>(new Models.PlayerProfileData(
            "PreviewPlayer", null, 120, 80, 5, 205, 3250, 42, 8.5, 5.2, 10.1, 0.0, 9 * 3600 + 32 * 60,
            new List<Models.AspectData>
            {
                new("OPTIMIST", 45),
                new("FRIENDLY", 30),
                new("TALKATIVE", 20),
                new("CLOWN", 10),
                new("TOXIC", 5),
            }));

    public Task<IReadOnlyList<Models.HeroProfileData>> GetHeroStatsAsync(string steamId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Models.HeroProfileData>>([
            new Models.HeroProfileData("Invoker", 34, 55.88, 7.04),
            new Models.HeroProfileData("Alchemist", 28, 50.00, 3.85),
            new Models.HeroProfileData("Pudge", 25, 56.00, 3.08),
        ]);

    public Task AbandonGameAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AcknowledgeNotificationAsync(string notificationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<Models.ChatReactionData>> ReactToMessageAsync(string messageId, int emoticonId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Models.ChatReactionData>>(Array.Empty<Models.ChatReactionData>());
    public Task<IReadOnlyList<d2c_launcher.Api.NotificationDto>> GetNotificationsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<d2c_launcher.Api.NotificationDto>>(Array.Empty<d2c_launcher.Api.NotificationDto>());

    public Task<IReadOnlyList<d2c_launcher.Api.LiveMatchDto>> GetLiveMatchesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<d2c_launcher.Api.LiveMatchDto>>([]);

    public async IAsyncEnumerable<d2c_launcher.Api.LiveMatchDto> SubscribeLiveMatchAsync(
        int matchId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public async IAsyncEnumerable<ChatMessageData> SubscribeChatAsync(
        string threadId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Stub: suspend indefinitely so the SSE loop doesn't spin synchronously and block the UI thread.
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        yield break;
    }
}

internal sealed class StubEmoticonService : IEmoticonService
{
    public Task<EmoticonLoadResult> LoadEmoticonsAsync()
        => Task.FromResult(new EmoticonLoadResult());
}

internal sealed class StubHttpImageService : IHttpImageService
{
    public Task<byte[]?> LoadBytesAsync(string? url, CancellationToken cancellationToken = default)
        => Task.FromResult<byte[]?>(null);
}

internal sealed class StubGameLaunchSettingsStorage : IGameLaunchSettingsStorage
{
    private GameLaunchSettings _settings = new();
    public event Action? SettingsChanged;
    public GameLaunchSettings Get() => _settings;
    public void Save(GameLaunchSettings settings) { _settings = settings; SettingsChanged?.Invoke(); }
}

internal sealed class StubVideoSettingsProvider : IVideoSettingsProvider
{
    private VideoSettings _settings = new();
    public VideoSettings Get() => _settings;
    public void Update(VideoSettings settings) { _settings = settings; }
    public void LoadFromVideoTxt(string gameDirectory) { }
}

internal sealed class StubCvarSettingsProvider : ICvarSettingsProvider
{
    private CvarSettings _settings = new();
    public bool IsGameRunning { get; set; }
    public event Action? CvarChanged;
    public CvarSettings Get() => _settings;
    public void Update(CvarSettings settings) { _settings = settings; CvarChanged?.Invoke(); }
    public bool LoadFromConfigCfg(string gameDirectory) => false;
    public IReadOnlyDictionary<string, string> GetPresetCvars() => new Dictionary<string, string>();
}

internal sealed class StubSettingsStorage : ISettingsStorage
{
    public LauncherSettings Get() => new();
    public void Save(LauncherSettings settings) { }
}

internal sealed class StubSteamAuthApi : ISteamAuthApi
{
    public Task<string?> ExchangeSteamSessionTicketAsync(string ticket, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task<string?> RefreshTokenAsync(string currentToken, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}

internal sealed class StubLocalManifestService : ILocalManifestService
{
    public Task<GameManifest> BuildAsync(string gameDirectory, IProgress<(int done, int total)>? progress = null, CancellationToken ct = default)
        => Task.FromResult(new GameManifest());
}

internal sealed class StubManifestDiffService : IManifestDiffService
{
    public IReadOnlyList<GameManifestFile> ComputeFilesToDownload(GameManifest remote, GameManifest local)
        => [];
}

internal sealed class StubGameDownloadService : IGameDownloadService
{
    public Task DownloadFilesAsync(IReadOnlyList<GameManifestFile> files, string gameDirectory, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
        => Task.CompletedTask;
}

internal sealed class StubContentRegistryService : IContentRegistryService
{
    private static readonly ContentRegistry StubRegistry = new()
    {
        Packages =
        [
            new() { Id = "base",   Folder = "base",   Name = "Dotaclassic",                           Optional = false },
            new() { Id = "redist", Folder = "redist", Name = "Необходимые библиотеки",                Optional = false },
            new() { Id = "ru_vo",  Folder = "ru_vo",  Name = "Русская озвучка от Strategic Music",    Optional = true  },
        ]
    };

    public Task<ContentRegistry?> GetAsync() => Task.FromResult<ContentRegistry?>(StubRegistry);
    public void Invalidate() { }
}

internal sealed class StubWindowService : IWindowService
{
    public bool IsWindowVisible => true;
#pragma warning disable CS0067
    public event Action? WindowShown;
#pragma warning restore CS0067
    public void ShowAndActivate() { }
}

internal sealed class StubUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

internal sealed class StubChatViewModelFactory : IChatViewModelFactory
{
    public d2c_launcher.ViewModels.ChatViewModel Create(string threadId)
        => new(threadId, new StubBackendApiService(), new StubHttpImageService(), new StubEmoticonService(), new StubQueueSocketService(), new StubWindowService());
}

internal sealed class StubNetConService : INetConService
{
#pragma warning disable CS0067
    public event Action<string>? LineReceived;
#pragma warning restore CS0067
    public bool IsConnected => false;
    public Task StartConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Disconnect() { }
    public Task SendCommandAsync(string command) => Task.CompletedTask;
    public Task WaitConnectedAsync(CancellationToken ct = default) => Task.Delay(Timeout.Infinite, ct);
    public void Dispose() { }
}

internal sealed class StubTriviaRepository : ITriviaRepository
{
    public Task<d2c_launcher.Models.TriviaQuestion[]> LoadAsync()
        => Task.FromResult<d2c_launcher.Models.TriviaQuestion[]>([
            new d2c_launcher.Models.ItemRecipeQuestion
            {
                Id          = "recipe_bkb",
                TargetItem  = "black_king_bar",
                Ingredients = ["ogre_axe", "mithril_hammer", "recipe"],
                Distractors = ["belt_of_strength", "claymore", "broadsword", "blade_of_alacrity"],
            },
            new d2c_launcher.Models.MultipleChoiceQuestion
            {
                Id           = "max_level",
                Question     = "Максимальный уровень героя?",
                Answers      = ["25", "30", "20", "28"],
                CorrectIndex = 0,
            },
        ]);
}
