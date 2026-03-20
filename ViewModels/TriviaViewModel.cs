using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class TriviaViewModel : ObservableObject
{
    private static readonly IBrush CorrectFgBrush = new SolidColorBrush(Color.Parse("#4CAF50"));
    private static readonly IBrush WrongFgBrush   = new SolidColorBrush(Color.Parse("#F44336"));
    private static readonly IBrush TimerFgBrush   = new SolidColorBrush(Color.Parse("#888888"));

    private readonly ITriviaRepository _repository;
    private readonly DispatcherTimer _countdownTimer;
    private DispatcherTimer? _advanceTimer;
    private TriviaQuestion[] _questions = [];
    private int[] _shuffledIndices = [];
    private int _questionIndex = -1;
    private readonly Random _rng = new();

    // ── Shared state ─────────────────────────────────────────────────────────

    [ObservableProperty] private bool _isItemRecipe;
    [ObservableProperty] private int _score;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeaderRightText))]
    private int _timerSeconds = TriviaTimerSeconds;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeaderRightText))]
    [NotifyPropertyChangedFor(nameof(HeaderRightForeground))]
    private bool _isAnswered;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnswerFeedbackText))]
    [NotifyPropertyChangedFor(nameof(AnswerFeedbackForeground))]
    [NotifyPropertyChangedFor(nameof(HeaderRightText))]
    [NotifyPropertyChangedFor(nameof(HeaderRightForeground))]
    private bool? _lastAnswerCorrect;

    public string ScoreText => I18n.T("trivia.score", ("score", Score.ToString()));
    public string TimerText => $"{TimerSeconds}с";
    public string AnswerFeedbackText => LastAnswerCorrect == true
        ? I18n.T("trivia.correct")
        : I18n.T("trivia.wrong");
    public IBrush AnswerFeedbackForeground => LastAnswerCorrect == true ? CorrectFgBrush : WrongFgBrush;

    /// <summary>Right side of the score row: shows countdown during question, feedback text during result.</summary>
    public string   HeaderRightText       => IsAnswered ? AnswerFeedbackText       : TimerText;
    public IBrush   HeaderRightForeground => IsAnswered ? AnswerFeedbackForeground : TimerFgBrush;

    // ── Recipe state ─────────────────────────────────────────────────────────

    [ObservableProperty] private string _targetItemImageUri = "";
    [ObservableProperty] private string _recipeQuestionText = "";

    public ObservableCollection<TriviaRecipeSlotVm> Slots { get; } = [];
    public ObservableCollection<TriviaPoolItemVm>   Pool  { get; } = [];

    // ── Multiple choice state ────────────────────────────────────────────────

    [ObservableProperty] private string _questionText = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSubjectItem))]
    private string _subjectItemImageUri = "";
    public bool HasSubjectItem => !string.IsNullOrEmpty(SubjectItemImageUri);

    public ObservableCollection<TriviaMcAnswerVm> Answers { get; } = [];

    // ── Constants ────────────────────────────────────────────────────────────

    public const int TriviaTimerSeconds = 20;

    /// <summary>Fired whenever a new question starts. Argument is the full duration in seconds.</summary>
    public event Action<int>? QuestionStarted;
    private const int FeedbackDelayMs = 7000;

    public TriviaViewModel(ITriviaRepository repository)
    {
        _repository = repository;

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += OnCountdownTick;
    }

    public async Task StartAsync()
    {
        if (_questions.Length == 0)
        {
            _questions = await _repository.LoadAsync();
            if (_questions.Length == 0) return;
        }

        _questionIndex = -1;
        _shuffledIndices = [];
        Score = 0;
        PickNextQuestion();
    }

    public void Stop()
    {
        _countdownTimer.Stop();
        _advanceTimer?.Stop();
        _advanceTimer = null;

        Score = 0;
        IsAnswered = false;
        LastAnswerCorrect = null;
        TimerSeconds = TriviaTimerSeconds;
        _questionIndex = -1;
        _shuffledIndices = [];

        Slots.Clear();
        Pool.Clear();
        Answers.Clear();
        QuestionText = "";
        TargetItemImageUri = "";
    }

    // ── Pool item click (recipe type) ────────────────────────────────────────

    public void SelectPoolItem(TriviaPoolItemVm item)
    {
        if (IsAnswered || item.Result != TriviaAnswerResult.None) return;

        if (item.IsSelected)
        {
            // Deselect: return the item to the pool and clear its slot
            item.IsSelected = false;
            if (item.AssignedSlot != null)
            {
                item.AssignedSlot.FilledImageUri = null;
                item.AssignedSlot = null;
            }
        }
        else
        {
            // Select: place into the next empty slot
            var slot = Slots.FirstOrDefault(s => !s.IsFilled);
            if (slot == null) return;

            item.IsSelected = true;
            item.AssignedSlot = slot;
            slot.FilledImageUri = item.ImageUri;

            if (Pool.Count(p => p.IsSelected) == Slots.Count)
                EvaluateRecipeAnswer();
        }
    }

    private void EvaluateRecipeAnswer()
    {
        var allCorrect = Pool.Where(p => p.IsSelected).All(p => p.IsCorrect);

        foreach (var item in Pool)
            item.Result = item.IsCorrect ? TriviaAnswerResult.Correct : TriviaAnswerResult.Wrong;

        ShowResult(allCorrect);
    }

    // ── MC answer click ──────────────────────────────────────────────────────

    public void SelectMcAnswer(TriviaMcAnswerVm answer)
    {
        if (IsAnswered) return;

        if (answer.Result == TriviaAnswerResult.None)
        {
            var correct = answer.Index == _mcCorrectIndex;
            answer.Result = correct ? TriviaAnswerResult.Correct : TriviaAnswerResult.Wrong;
            if (!correct)
            {
                var correctAnswer = Answers.FirstOrDefault(a => a.Index == _mcCorrectIndex);
                if (correctAnswer != null) correctAnswer.Result = TriviaAnswerResult.Correct;
            }
            ShowResult(correct);
        }
    }

    private int _mcCorrectIndex;

    // ── Internal ─────────────────────────────────────────────────────────────

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        TimerSeconds--;
        OnPropertyChanged(nameof(TimerText));
        if (TimerSeconds <= 0)
        {
            _countdownTimer.Stop();
            ShowResult(false);
        }
    }

    private void ShowResult(bool correct)
    {
        _countdownTimer.Stop();
        IsAnswered = true;
        LastAnswerCorrect = correct;
        if (correct) Score++;
        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(AnswerFeedbackText));
        OnPropertyChanged(nameof(AnswerFeedbackForeground));

        _advanceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FeedbackDelayMs) };
        _advanceTimer.Tick += (_, _) =>
        {
            _advanceTimer!.Stop();
            _advanceTimer = null;
            IsAnswered = false;
            LastAnswerCorrect = null;
            TimerSeconds = TriviaTimerSeconds;
            OnPropertyChanged(nameof(TimerText));
            PickNextQuestion();
        };
        _advanceTimer.Start();
    }

    private void PickNextQuestion()
    {
        if (_questions.Length == 0) return;

        if (_shuffledIndices.Length == 0 || _questionIndex >= _shuffledIndices.Length - 1)
        {
            _shuffledIndices = Enumerable.Range(0, _questions.Length)
                .OrderBy(_ => _rng.Next())
                .ToArray();
            _questionIndex = 0;
        }
        else
        {
            _questionIndex++;
        }

        LoadQuestion(_questions[_shuffledIndices[_questionIndex]]);
    }

    private void LoadQuestion(TriviaQuestion question)
    {
        Slots.Clear();
        Pool.Clear();
        Answers.Clear();

        if (question is ItemRecipeQuestion recipe)
        {
            IsItemRecipe = true;
            TargetItemImageUri = DotaItemData.GetItemImageUrlByName(recipe.TargetItem) ?? "";
            RecipeQuestionText = I18n.T("trivia.recipeQuestion");

            foreach (var _ in recipe.Ingredients)
                Slots.Add(new TriviaRecipeSlotVm());

            // Build shuffled pool: ingredients + distractors
            var pool = new List<TriviaPoolItemVm>();
            foreach (var key in recipe.Ingredients)
            {
                pool.Add(new TriviaPoolItemVm
                {
                    ItemKey   = key,
                    ImageUri  = DotaItemData.GetItemImageUrlByName(key) ?? "",
                    IsCorrect = true,
                });
            }
            foreach (var key in recipe.Distractors)
            {
                pool.Add(new TriviaPoolItemVm
                {
                    ItemKey   = key,
                    ImageUri  = DotaItemData.GetItemImageUrlByName(key) ?? "",
                    IsCorrect = false,
                });
            }
            foreach (var item in pool.OrderBy(_ => _rng.Next()))
                Pool.Add(item);
        }
        else if (question is MultipleChoiceQuestion mc)
        {
            IsItemRecipe = false;
            QuestionText = mc.Question;
            _mcCorrectIndex = mc.CorrectIndex;
            SubjectItemImageUri = mc.ItemKey != null
                ? DotaItemData.GetItemImageUrlByName(mc.ItemKey) ?? ""
                : "";

            for (int i = 0; i < mc.Answers.Length; i++)
                Answers.Add(new TriviaMcAnswerVm { Text = mc.Answers[i], Index = i });
        }

        TimerSeconds = TriviaTimerSeconds;
        OnPropertyChanged(nameof(TimerText));
        QuestionStarted?.Invoke(TriviaTimerSeconds);
        _countdownTimer.Start();
    }
}
