using CommunityToolkit.Mvvm.ComponentModel;

namespace d2c_launcher.ViewModels;

public enum TriviaAnswerResult { None, Correct, Wrong }

public partial class TriviaMcAnswerVm : ObservableObject
{
    public string Text { get; init; } = "";
    public int Index { get; init; }

    [ObservableProperty] private TriviaAnswerResult _result = TriviaAnswerResult.None;
}
