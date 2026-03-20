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
    private static readonly IBrush CorrectBrush = new SolidColorBrush(Color.Parse("#1B4A1B"));
    private static readonly IBrush WrongBrush   = new SolidColorBrush(Color.Parse("#4A1B1B"));

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
    [ObservableProperty] private int _timerSeconds = TriviaTimerSeconds;
    [ObservableProperty] private bool _isAnswered;
    [ObservableProperty] private bool? _lastAnswerCorrect;

    public string ScoreText => I18n.T("trivia.score", ("score", Score.ToString()));
    public string TimerText => $"{TimerSeconds}с";
    public string GuessesText => I18n.T("trivia.guesses", ("n", GuessesLeft.ToString()));
    public string AnswerFeedbackText => LastAnswerCorrect == true
        ? I18n.T("trivia.correct")
        : I18n.T("trivia.wrong");
    public IBrush AnswerFeedbackBackground => LastAnswerCorrect == true ? CorrectBrush : WrongBrush;

    // ── Recipe state ─────────────────────────────────────────────────────────

    [ObservableProperty] private string _targetItemImageUri = "";
    [ObservableProperty] private string _recipeQuestionText = "";
    [ObservableProperty] private int _guessesLeft = MaxGuesses;

    public ObservableCollection<TriviaRecipeSlotVm> Slots { get; } = [];
    public ObservableCollection<TriviaPoolItemVm>   Pool  { get; } = [];

    // ── Multiple choice state ────────────────────────────────────────────────

    [ObservableProperty] private string _questionText = "";

    public ObservableCollection<TriviaMcAnswerVm> Answers { get; } = [];

    // ── Constants ────────────────────────────────────────────────────────────

    public const int TriviaTimerSeconds = 20;

    /// <summary>Fired whenever a new question starts. Argument is the full duration in seconds.</summary>
    public event Action<int>? QuestionStarted;
    private const int MaxGuesses = 3;
    private const int FeedbackDelayMs = 1500;

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
        if (IsAnswered || item.IsUsed) return;

        item.IsUsed = true;

        if (item.IsCorrect)
        {
            // Fill the next empty slot
            var slot = Slots.FirstOrDefault(s => !s.IsFilled);
            if (slot != null)
                slot.FilledImageUri = item.ImageUri;

            // Check if all slots are filled
            if (Slots.All(s => s.IsFilled))
                ShowResult(true);
        }
        else
        {
            GuessesLeft--;
            OnPropertyChanged(nameof(GuessesText));
            if (GuessesLeft <= 0)
                ShowResult(false);
        }
    }

    // ── MC answer click ──────────────────────────────────────────────────────

    public void SelectMcAnswer(TriviaMcAnswerVm answer)
    {
        if (IsAnswered) return;

        if (answer.Result == TriviaAnswerResult.None)
        {
            var correct = answer.Index == (_mcCorrectIndex);
            answer.Result = correct ? TriviaAnswerResult.Correct : TriviaAnswerResult.Wrong;
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
        OnPropertyChanged(nameof(AnswerFeedbackBackground));

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
            GuessesLeft = MaxGuesses;
            OnPropertyChanged(nameof(GuessesText));

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

            for (int i = 0; i < mc.Answers.Length; i++)
                Answers.Add(new TriviaMcAnswerVm { Text = mc.Answers[i], Index = i });
        }

        TimerSeconds = TriviaTimerSeconds;
        OnPropertyChanged(nameof(TimerText));
        QuestionStarted?.Invoke(TriviaTimerSeconds);
        _countdownTimer.Start();
    }
}
