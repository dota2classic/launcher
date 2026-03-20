using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.ViewModels;

public enum TriviaAnswerResult { None, Correct, Wrong }

public partial class TriviaMcAnswerVm : ObservableObject
{
    public string Text { get; init; } = "";
    public int Index { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWrong))]
    [NotifyPropertyChangedFor(nameof(AnswerForeground))]
    private TriviaAnswerResult _result = TriviaAnswerResult.None;

    public bool IsWrong => Result == TriviaAnswerResult.Wrong;

    // Dim text for wrong answers; Correct/None keep normal colour
    public string AnswerForeground => Result == TriviaAnswerResult.Wrong ? "#4a5260" : "#D9D9D9";
}
