using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.ViewModels;

public partial class TriviaPoolItemVm : ObservableObject
{
    private static readonly IBrush BgDefault  = new SolidColorBrush(Colors.Transparent);
    private static readonly IBrush BgSelected = new SolidColorBrush(Color.Parse("#1a2d3a"));
    private static readonly IBrush BgCorrect  = new SolidColorBrush(Color.Parse("#2e7d32"));
    private static readonly IBrush BgWrong    = new SolidColorBrush(Color.Parse("#8b1a1a"));

    private static readonly IBrush BorderDefault  = new SolidColorBrush(Color.Parse("#2d3842"));
    private static readonly IBrush BorderSelected = new SolidColorBrush(Color.Parse("#C8A84B"));

    public string ItemKey  { get; init; } = "";
    public string ImageUri { get; init; } = "";
    internal bool IsCorrect { get; init; }
    internal TriviaRecipeSlotVm? AssignedSlot { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemBackground))]
    [NotifyPropertyChangedFor(nameof(SelectionBorderBrush))]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemBackground))]
    [NotifyPropertyChangedFor(nameof(SelectionBorderBrush))]
    private TriviaAnswerResult _result = TriviaAnswerResult.None;

    public IBrush ItemBackground => Result switch
    {
        TriviaAnswerResult.Wrong   => BgWrong,
        TriviaAnswerResult.Correct => BgCorrect,
        _                          => IsSelected ? BgSelected : BgDefault,
    };

    public IBrush SelectionBorderBrush =>
        Result == TriviaAnswerResult.None && IsSelected ? BorderSelected : BorderDefault;
}
