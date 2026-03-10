using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Api;
using d2c_launcher.Models;
using d2c_launcher.Services;

namespace d2c_launcher.Preview;

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

    public Task PostChatMessageAsync(string threadId, string content, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<EmoticonData>> GetEmoticonsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<EmoticonData>>([]);

    public Task<Models.LiveMatchInfo?> GetLiveMatchAsync(int matchId, CancellationToken cancellationToken = default)
        => Task.FromResult<Models.LiveMatchInfo?>(null);

    public async IAsyncEnumerable<ChatMessageData> SubscribeChatAsync(
        string threadId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Stub: yield nothing (preview shows only the initially-loaded messages).
        await Task.CompletedTask;
        yield break;
    }
}

internal sealed class StubEmoticonService : IEmoticonService
{
    public Task<Dictionary<string, byte[]>> GetEmoticonImagesAsync()
        => Task.FromResult(new Dictionary<string, byte[]>());
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
