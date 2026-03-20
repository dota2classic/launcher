using System.Threading.Tasks;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Tests.Fakes;
using d2c_launcher.ViewModels;
using Xunit;

namespace d2c_launcher.Tests;

public sealed class TriviaViewModelTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (TriviaViewModel vm, FakeTimerFactory timers) Build(params TriviaQuestion[] questions)
    {
        var timers = new FakeTimerFactory();
        var vm = new TriviaViewModel(new StubRepo(questions), timers);
        return (vm, timers);
    }

    private static ItemRecipeQuestion Recipe(string target, string[] ingredients, string[] distractors) =>
        new() { Id = target, TargetItem = target, Ingredients = ingredients, Distractors = distractors };

    private static MultipleChoiceQuestion Mc(string question, string[] answers, int correctIndex, string? itemKey = null) =>
        new() { Id = question, Question = question, Answers = answers, CorrectIndex = correctIndex, ItemKey = itemKey };

    // ── Recipe: loading ───────────────────────────────────────────────────────

    [Fact]
    public async Task Recipe_LoadQuestion_CreatesCorrectSlotsAndPool()
    {
        var (vm, _) = Build(Recipe("bfury", ["broadsword", "claymore", "recipe_bfury"], ["chainmail"]));

        await vm.StartAsync();

        Assert.True(vm.IsItemRecipe);
        Assert.Equal(3, vm.Slots.Count);               // one slot per ingredient
        Assert.Equal(4, vm.Pool.Count);                // 3 ingredients + 1 distractor
        Assert.Equal(3, CountWhere(vm.Pool, p => p.IsCorrect));
        Assert.Equal(1, CountWhere(vm.Pool, p => !p.IsCorrect));
    }

    [Fact]
    public async Task Recipe_AllSlotsFilledWithCorrect_Scores()
    {
        var (vm, timers) = Build(Recipe("bfury", ["broadsword", "claymore"], ["chainmail"]));
        await vm.StartAsync();

        // Select only the two correct items
        foreach (var item in vm.Pool)
            if (item.IsCorrect)
                vm.SelectPoolItem(item);

        Assert.True(vm.IsAnswered);
        Assert.Equal(true, vm.LastAnswerCorrect);
        Assert.Equal(1, vm.Score);
    }

    [Fact]
    public async Task Recipe_SelectingDistractor_DoesNotScore()
    {
        var (vm, _) = Build(Recipe("bfury", ["broadsword"], ["chainmail"]));
        await vm.StartAsync();

        // Select the distractor first so it fills the only slot
        var distractor = FindFirst(vm.Pool, p => !p.IsCorrect);
        vm.SelectPoolItem(distractor);

        Assert.True(vm.IsAnswered);
        Assert.Equal(false, vm.LastAnswerCorrect);
        Assert.Equal(0, vm.Score);
    }

    [Fact]
    public async Task Recipe_Deselect_ClearsSlotAndAllowsReselect()
    {
        var (vm, _) = Build(Recipe("bfury", ["broadsword", "claymore"], ["chainmail"]));
        await vm.StartAsync();

        var item = FindFirst(vm.Pool, p => p.IsCorrect);
        vm.SelectPoolItem(item);   // select → fills slot
        Assert.True(item.IsSelected);
        var assignedSlot = item.AssignedSlot;
        Assert.NotNull(assignedSlot);
        Assert.NotNull(assignedSlot.FilledImageUri); // slot is filled

        vm.SelectPoolItem(item);   // deselect
        Assert.False(item.IsSelected);
        Assert.Null(item.AssignedSlot);
        Assert.Null(assignedSlot.FilledImageUri); // slot was cleared
        Assert.False(vm.IsAnswered);
    }

    [Fact]
    public async Task Recipe_SelectPoolItem_WhenAnswered_IsNoop()
    {
        var (vm, timers) = Build(Recipe("bfury", ["broadsword"], ["chainmail"]));
        await vm.StartAsync();

        // Answer it (select distractor to trigger a wrong result)
        var distractor = FindFirst(vm.Pool, p => !p.IsCorrect);
        vm.SelectPoolItem(distractor);
        Assert.True(vm.IsAnswered);

        int scoreBefore = vm.Score;
        var anotherItem = FindFirst(vm.Pool, p => p.IsCorrect);
        vm.SelectPoolItem(anotherItem);  // should be a no-op
        Assert.Equal(scoreBefore, vm.Score);
    }

    // ── MC: loading ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Mc_LoadQuestion_PopulatesAnswers()
    {
        var (vm, _) = Build(Mc("Who created Dota?", ["Icefrog", "Valve", "Blizzard", "Eul"], 0));

        await vm.StartAsync();

        Assert.False(vm.IsItemRecipe);
        Assert.Equal(4, vm.Answers.Count);
        Assert.Equal("Who created Dota?", vm.QuestionText);
    }

    [Fact]
    public async Task Mc_LoadQuestion_WithItemKey_SetsSubjectUri()
    {
        var (vm, _) = Build(Mc("What does this item do?", ["A", "B"], 0, itemKey: "chainmail"));

        await vm.StartAsync();

        Assert.True(vm.HasSubjectItem);
        Assert.Contains("chainmail", vm.SubjectItemImageUri);
    }

    [Fact]
    public async Task Mc_CorrectAnswer_ScoresAndSetsAnswered()
    {
        var (vm, _) = Build(Mc("Q", ["A", "B", "C"], correctIndex: 1));
        await vm.StartAsync();

        var correct = FindFirst(vm.Answers, a => a.Index == vm.McCorrectIndex);
        vm.SelectMcAnswer(correct);

        Assert.Equal(TriviaAnswerResult.Correct, correct.Result);
        Assert.True(vm.IsAnswered);
        Assert.Equal(true, vm.LastAnswerCorrect);
        Assert.Equal(1, vm.Score);
    }

    [Fact]
    public async Task Mc_WrongAnswer_NoScoreAndRevealsCorrect()
    {
        var (vm, _) = Build(Mc("Q", ["A", "B", "C"], correctIndex: 2));
        await vm.StartAsync();

        var wrong = FindFirst(vm.Answers, a => a.Index != vm.McCorrectIndex);
        vm.SelectMcAnswer(wrong);

        Assert.Equal(TriviaAnswerResult.Wrong, wrong.Result);
        Assert.Equal(TriviaAnswerResult.Correct, FindFirst(vm.Answers, a => a.Index == vm.McCorrectIndex).Result);
        Assert.True(vm.IsAnswered);
        Assert.Equal(false, vm.LastAnswerCorrect);
        Assert.Equal(0, vm.Score);
    }

    [Fact]
    public async Task Mc_SelectAnswer_WhenAnswered_IsNoop()
    {
        var (vm, _) = Build(Mc("Q", ["A", "B"], correctIndex: 0));
        await vm.StartAsync();

        var correct = FindFirst(vm.Answers, a => a.Index == vm.McCorrectIndex);
        var other   = FindFirst(vm.Answers, a => a.Index != vm.McCorrectIndex);

        vm.SelectMcAnswer(correct); // correct — sets IsAnswered
        Assert.True(vm.IsAnswered);

        int scoreBefore = vm.Score;
        vm.SelectMcAnswer(other); // should be a no-op
        Assert.Equal(scoreBefore, vm.Score);
        Assert.Equal(TriviaAnswerResult.None, other.Result);
    }

    // ── Timer: countdown ─────────────────────────────────────────────────────

    [Fact]
    public async Task Countdown_ReachesZero_RevealsAnswerAndSetsWrong()
    {
        var (vm, timers) = Build(Mc("Q", ["A", "B"], correctIndex: 0));
        await vm.StartAsync();

        // Drain the countdown
        for (int i = 0; i < TriviaViewModel.TriviaTimerSeconds; i++)
            timers.Countdown.Fire();

        Assert.True(vm.IsAnswered);
        Assert.Equal(false, vm.LastAnswerCorrect);
        Assert.Equal(0, vm.Score);
        // Correct answer must be revealed
        Assert.Equal(TriviaAnswerResult.Correct, FindFirst(vm.Answers, a => a.Index == vm.McCorrectIndex).Result);
    }

    [Fact]
    public async Task Countdown_StopsAfterAnswer()
    {
        var (vm, timers) = Build(Mc("Q", ["A", "B"], correctIndex: 0));
        await vm.StartAsync();

        vm.SelectMcAnswer(vm.Answers[0]); // answer it

        Assert.False(timers.Countdown.IsRunning);
    }

    // ── Timer: advance ────────────────────────────────────────────────────────

    [Fact]
    public async Task AdvanceTimer_FiresAfterResult_LoadsNextQuestion()
    {
        var (vm, timers) = Build(
            Mc("Q1", ["A", "B"], correctIndex: 0),
            Mc("Q2", ["C", "D"], correctIndex: 1));
        await vm.StartAsync();

        string firstQuestion = vm.QuestionText;
        vm.SelectMcAnswer(vm.Answers[0]); // answer → advance timer created

        // Fire the advance timer → next question loads
        timers.Latest.Fire();

        Assert.False(vm.IsAnswered);
        // Timer restarted for new question
        Assert.True(timers.Countdown.IsRunning);
    }

    [Fact]
    public async Task AdvanceTimer_ResetsState()
    {
        var (vm, timers) = Build(Mc("Q", ["A", "B"], correctIndex: 0));
        await vm.StartAsync();

        vm.SelectMcAnswer(vm.Answers[0]);
        Assert.True(vm.IsAnswered);

        timers.Latest.Fire(); // advance

        Assert.False(vm.IsAnswered);
        Assert.Null(vm.LastAnswerCorrect);
        Assert.Equal(TriviaViewModel.TriviaTimerSeconds, vm.TimerSeconds);
    }

    // ── Stop ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Stop_ResetsAllState()
    {
        var (vm, timers) = Build(Mc("Q", ["A", "B"], correctIndex: 0));
        await vm.StartAsync();

        vm.SelectMcAnswer(vm.Answers[0]); // score 1 — creates advance timer
        var advanceTimer = timers.Latest;
        vm.Stop();

        Assert.Equal(0, vm.Score);
        Assert.False(vm.IsAnswered);
        Assert.Null(vm.LastAnswerCorrect);
        Assert.Equal(TriviaViewModel.TriviaTimerSeconds, vm.TimerSeconds);
        Assert.Empty(vm.Answers);
        Assert.Empty(vm.Slots);
        Assert.Empty(vm.Pool);
        Assert.False(timers.Countdown.IsRunning);
        Assert.False(advanceTimer.IsRunning);
    }

    // ── Question cycling ─────────────────────────────────────────────────────

    [Fact]
    public async Task Questions_CycleThroughAllBeforeRepeating()
    {
        // Three distinct MC questions — collect which questions appear in first 3 rounds
        var q1 = Mc("Q1", ["A"], 0);
        var q2 = Mc("Q2", ["B"], 0);
        var q3 = Mc("Q3", ["C"], 0);
        var (vm, timers) = Build(q1, q2, q3);
        await vm.StartAsync();

        var seen = new HashSet<string>();
        for (int round = 0; round < 3; round++)
        {
            seen.Add(vm.QuestionText);
            vm.SelectMcAnswer(vm.Answers[0]); // answer
            timers.Latest.Fire();             // advance to next
        }

        // All three questions must have been shown
        Assert.Contains("Q1", seen);
        Assert.Contains("Q2", seen);
        Assert.Contains("Q3", seen);
    }

    [Fact]
    public async Task Questions_EmptyRepository_DoesNotCrash()
    {
        var (vm, _) = Build(); // no questions
        await vm.StartAsync(); // should be a no-op
        Assert.Equal(0, vm.Score);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static int CountWhere<T>(IEnumerable<T> source, Func<T, bool> pred)
        => source.Count(pred);

    private static T FindFirst<T>(IEnumerable<T> source, Func<T, bool> pred)
        => source.First(pred);

    // ── Stub repo ─────────────────────────────────────────────────────────────

    private sealed class StubRepo(params TriviaQuestion[] questions) : ITriviaRepository
    {
        public Task<TriviaQuestion[]> LoadAsync() => Task.FromResult(questions);
    }
}
