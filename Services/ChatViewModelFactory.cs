using d2c_launcher.ViewModels;

namespace d2c_launcher.Services;

public interface IChatViewModelFactory
{
    ChatViewModel Create(string threadId);
}

public sealed class ChatViewModelFactory : IChatViewModelFactory
{
    private readonly IBackendApiService _backendApiService;
    private readonly IHttpImageService _imageService;
    private readonly IEmoticonSnapshotBuilder _emoticonSnapshot;
    private readonly IUserNameResolver _userNameResolver;
    private readonly IQueueSocketService _queueSocketService;
    private readonly IWindowService _windowService;
    private readonly IUiDispatcher _dispatcher;

    public ChatViewModelFactory(
        IBackendApiService backendApiService,
        IHttpImageService imageService,
        IEmoticonSnapshotBuilder emoticonSnapshot,
        IUserNameResolver userNameResolver,
        IQueueSocketService queueSocketService,
        IWindowService windowService,
        IUiDispatcher dispatcher)
    {
        _backendApiService = backendApiService;
        _imageService = imageService;
        _emoticonSnapshot = emoticonSnapshot;
        _userNameResolver = userNameResolver;
        _queueSocketService = queueSocketService;
        _windowService = windowService;
        _dispatcher = dispatcher;
    }

    public ChatViewModel Create(string threadId)
        => new(
            threadId,
            _backendApiService,
            _imageService,
            _emoticonSnapshot,
            _userNameResolver,
            new ChatMessageStream(threadId, _backendApiService),
            _queueSocketService,
            _windowService,
            _dispatcher);
}
