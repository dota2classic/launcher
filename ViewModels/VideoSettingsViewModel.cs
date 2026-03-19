using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

public record ResolutionItem(string Label, int W, int H, bool IsNative);

public partial class VideoSettingsViewModel : ViewModelBase
{
    private readonly IVideoSettingsProvider _videoProvider;
    private readonly IGameLaunchSettingsStorage _launchStorage;

    // ── Video settings ─────────────────────────────────────────────────────────

    public bool Fullscreen
    {
        get => _videoProvider.Get().Fullscreen;
        set
        {
            var s = _videoProvider.Get();
            if (s.Fullscreen == value) return;
            s.Fullscreen = value;
            _videoProvider.Update(s);
            OnPropertyChanged();
        }
    }

    public bool NoWindowBorder
    {
        get => _videoProvider.Get().NoWindowBorder;
        set
        {
            var s = _videoProvider.Get();
            if (s.NoWindowBorder == value) return;
            s.NoWindowBorder = value;
            _videoProvider.Update(s);
            OnPropertyChanged();
        }
    }

    // ── Resolution picker ─────────────────────────────────────────────────────

    private static readonly (string Label, int W, int H)[] AllResolutions =
    [
        // 4:3
        ("4:3", 640, 480), ("4:3", 800, 600), ("4:3", 1024, 768), ("4:3", 1280, 960), ("4:3", 1600, 1200),
        // 5:4
        ("5:4", 1280, 1024),
        // 16:10
        ("16:10", 1280, 800), ("16:10", 1440, 900), ("16:10", 1680, 1050), ("16:10", 1920, 1200),
        // 16:9
        ("16:9", 1280, 720), ("16:9", 1366, 768), ("16:9", 1600, 900), ("16:9", 1920, 1080),
        ("16:9", 2560, 1440), ("16:9", 3840, 2160),
    ];

    public static string[] AvailableAspectRatios { get; } = ["4:3", "5:4", "16:10", "16:9"];

    private (int W, int H) _monitorSize = (0, 0);
    private int _selectedAspectRatioIndex = 3; // default 16:9

    public int SelectedAspectRatioIndex
    {
        get => _selectedAspectRatioIndex;
        set
        {
            if (_selectedAspectRatioIndex == value) return;
            _selectedAspectRatioIndex = value;
            var resolutions = ResolutionsForCurrentRatio();
            if (resolutions.Count > 0)
            {
                var vs = _videoProvider.Get();
                vs.Width = resolutions[0].W;
                vs.Height = resolutions[0].H;
                _videoProvider.Update(vs);
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(AvailableResolutions));
            OnPropertyChanged(nameof(SelectedResolution));
        }
    }

    public void SetMonitorSize(int w, int h)
    {
        _monitorSize = (w, h);
        var ratio = InferAspectRatio(w, h);
        var ratioIdx = System.Array.IndexOf(AvailableAspectRatios, ratio);
        if (ratioIdx >= 0) _selectedAspectRatioIndex = ratioIdx;
        OnPropertyChanged(nameof(SelectedAspectRatioIndex));
        OnPropertyChanged(nameof(AvailableResolutions));
        OnPropertyChanged(nameof(SelectedResolution));
    }

    private static string InferAspectRatio(int w, int h)
    {
        if (h == 0) return "16:9";
        var r = (double)w / h;
        if (r < 1.28) return "5:4";   // ~1.25
        if (r < 1.40) return "4:3";   // ~1.333
        if (r < 1.68) return "16:10"; // ~1.6
        return "16:9";                 // ~1.778+
    }

    private List<(string Label, int W, int H)> ResolutionsForCurrentRatio()
    {
        var ratio = AvailableAspectRatios[_selectedAspectRatioIndex];
        return AllResolutions.Where(r => r.Label == ratio).ToList();
    }

    public ResolutionItem[] AvailableResolutions =>
        ResolutionsForCurrentRatio()
            .Select(r => new ResolutionItem($"{r.W}×{r.H}", r.W, r.H, (r.W, r.H) == _monitorSize))
            .ToArray();

    public ResolutionItem? SelectedResolution
    {
        get
        {
            var list = ResolutionsForCurrentRatio();
            var s = _videoProvider.Get();
            var match = list.Find(r => r.W == s.Width && r.H == s.Height);
            var entry = match != default ? match : (list.Count > 0 ? list[0] : default);
            if (entry == default) return null;
            return new ResolutionItem($"{entry.W}×{entry.H}", entry.W, entry.H, (entry.W, entry.H) == _monitorSize);
        }
        set
        {
            if (value == null) return;
            var s = _videoProvider.Get();
            if (s.Width == value.W && s.Height == value.H) return;
            s.Width = value.W;
            s.Height = value.H;
            _videoProvider.Update(s);
            OnPropertyChanged();
        }
    }

    public void RefreshFromVideoProvider()
    {
        OnPropertyChanged(nameof(Fullscreen));
        OnPropertyChanged(nameof(NoWindowBorder));
        OnPropertyChanged(nameof(SelectedResolution));
    }

    // ── Launch settings ───────────────────────────────────────────────────────

    public static string[] AvailableLanguages { get; } = ["russian", "english"];

    public string SelectedLanguage
    {
        get => _launchStorage.Get().Language;
        set
        {
            var s = _launchStorage.Get();
            if (s.Language == value) return;
            s.Language = value;
            _launchStorage.Save(s);
            OnPropertyChanged();
        }
    }

    public bool NoVid
    {
        get => _launchStorage.Get().NoVid;
        set
        {
            var s = _launchStorage.Get();
            if (s.NoVid == value) return;
            s.NoVid = value;
            _launchStorage.Save(s);
            OnPropertyChanged();
        }
    }

    public string ExtraArgs
    {
        get => _launchStorage.Get().ExtraArgs ?? "";
        set
        {
            var s = _launchStorage.Get();
            var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (s.ExtraArgs == trimmed) return;
            s.ExtraArgs = trimmed;
            _launchStorage.Save(s);
            OnPropertyChanged();
        }
    }

    // ── Constructor ────────────────────────────────────────────────────────────

    public VideoSettingsViewModel(IVideoSettingsProvider videoProvider, IGameLaunchSettingsStorage launchStorage)
    {
        _videoProvider = videoProvider;
        _launchStorage = launchStorage;
    }
}
