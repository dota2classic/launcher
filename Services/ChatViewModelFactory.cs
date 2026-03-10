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
    private readonly IEmoticonService _emoticonService;
    private readonly IQueueSocketService _queueSocketService;
    private readonly IWindowService _windowService;

    public ChatViewModelFactory(
        IBackendApiService backendApiService,
        IHttpImageService imageService,
        IEmoticonService emoticonService,
        IQueueSocketService queueSocketService,
        IWindowService windowService)
    {
        _backendApiService = backendApiService;
        _imageService = imageService;
        _emoticonService = emoticonService;
        _queueSocketService = queueSocketService;
        _windowService = windowService;
    }

    public ChatViewModel Create(string threadId)
        => new(threadId, _backendApiService, _imageService, _emoticonService, _queueSocketService, _windowService);
}
