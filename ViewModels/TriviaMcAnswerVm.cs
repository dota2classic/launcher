using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.ViewModels;

public enum TriviaAnswerResult { None, Correct, Wrong }

public partial class TriviaMcAnswerVm : ObservableObject
{
    private static readonly IBrush BorderCorrect = new SolidColorBrush(Color.Parse("#2e7d32"));
    private static readonly IBrush BorderWrong   = new SolidColorBrush(Color.Parse("#7f1d1d"));
    private static readonly IBrush BorderNone    = new SolidColorBrush(Color.Parse("#2d3842"));

    public string Text { get; init; } = "";
    public int Index { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCorrect))]
    [NotifyPropertyChangedFor(nameof(IsWrong))]
    [NotifyPropertyChangedFor(nameof(AnswerForeground))]
    [NotifyPropertyChangedFor(nameof(ButtonBorderBrush))]
    private TriviaAnswerResult _result = TriviaAnswerResult.None;

    public bool IsCorrect => Result == TriviaAnswerResult.Correct;
    public bool IsWrong   => Result == TriviaAnswerResult.Wrong;

    // Dim text for wrong answers; Correct/None keep normal colour
    public string AnswerForeground => Result == TriviaAnswerResult.Wrong ? "#4a5260" : "#D9D9D9";

    public IBrush ButtonBorderBrush => Result switch
    {
        TriviaAnswerResult.Correct => BorderCorrect,
        TriviaAnswerResult.Wrong   => BorderWrong,
        _                          => BorderNone,
    };
}
