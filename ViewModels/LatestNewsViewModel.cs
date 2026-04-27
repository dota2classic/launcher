using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Api;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class LatestNewsViewModel : ObservableObject
{
    private const string BlogBaseUrl = "https://dotaclassic.ru/blog/";
    private static readonly CultureInfo RuCulture = new("ru-RU");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPost))]
    [NotifyPropertyChangedFor(nameof(PostUrl))]
    [NotifyPropertyChangedFor(nameof(FormattedPublishDate))]
    private BlogpostDto? _post;

    public bool HasPost => Post != null;
    public string? PostUrl => Post != null ? BlogBaseUrl + (int)Post.Id : null;
    public string? FormattedPublishDate => FormatDate(Post?.PublishDate);

    private static string? FormatDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTimeOffset.TryParse(raw, null, DateTimeStyles.RoundtripKind, out var dto))
            return dto.ToString("d MMMM yyyy", RuCulture);
        if (DateTime.TryParse(raw, out var dt))
            return dt.ToString("d MMMM yyyy", RuCulture);
        return raw;
    }

    public LatestNewsViewModel(IBackendApiService backendApiService)
    {
        LoadAsync(backendApiService).FireAndForget("LatestNewsViewModel.LoadAsync");
    }

    private async Task LoadAsync(IBackendApiService backendApiService)
    {
        var post = await backendApiService.GetLatestBlogPostAsync().ConfigureAwait(false);
        Dispatcher.UIThread.Post(() => Post = post);
    }

    [RelayCommand]
    private void OpenPost()
    {
        if (PostUrl is not { } url) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
