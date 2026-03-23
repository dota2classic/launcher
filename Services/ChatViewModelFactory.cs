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
    private readonly IChatMessageStreamFactory _streamFactory;
    private readonly IQueueSocketService _queueSocketService;
    private readonly IWindowService _windowService;

    public ChatViewModelFactory(
        IBackendApiService backendApiService,
        IHttpImageService imageService,
        IEmoticonSnapshotBuilder emoticonSnapshot,
        IUserNameResolver userNameResolver,
        IChatMessageStreamFactory streamFactory,
        IQueueSocketService queueSocketService,
        IWindowService windowService)
    {
        _backendApiService = backendApiService;
        _imageService = imageService;
        _emoticonSnapshot = emoticonSnapshot;
        _userNameResolver = userNameResolver;
        _streamFactory = streamFactory;
        _queueSocketService = queueSocketService;
        _windowService = windowService;
    }

    public ChatViewModel Create(string threadId)
        => new(
            threadId,
            _backendApiService,
            _imageService,
            _emoticonSnapshot,
            _userNameResolver,
            _streamFactory.Create(threadId),
            _queueSocketService,
            _windowService);
}
