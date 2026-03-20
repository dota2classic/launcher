using System;
using System.ComponentModel;
using System.Threading;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Styling;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Views.Components;

public partial class TriviaPanel : UserControl
{
    private static readonly IBrush FlashCorrect = new SolidColorBrush(Color.Parse("#4CAF50"));
    private static readonly IBrush FlashWrong   = new SolidColorBrush(Color.Parse("#F44336"));

    private TriviaViewModel? _vm;
    private CancellationTokenSource? _flashCts;
    private CancellationTokenSource? _fadeCts;

    public TriviaPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();
        Subscribe(DataContext as TriviaViewModel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unsubscribe();
        _flashCts?.Cancel();
        _fadeCts?.Cancel();
    }

    private void Subscribe(TriviaViewModel? vm)
    {
        if (vm == null) return;
        _vm = vm;
        vm.PropertyChanged += OnVmPropertyChanged;
        vm.QuestionStarted += OnQuestionStarted;
    }

    private void Unsubscribe()
    {
        if (_vm == null) return;
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm.QuestionStarted -= OnQuestionStarted;
        _vm = null;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TriviaViewModel.LastAnswerCorrect) &&
            _vm?.LastAnswerCorrect != null)
        {
            RunResultFlash(_vm.LastAnswerCorrect == true);
        }
    }

    private void OnQuestionStarted(int _) => RunContentFadeIn();

    // ── Result flash ─────────────────────────────────────────────────────────

    private void RunResultFlash(bool correct)
    {
        if (ResultOverlay == null) return;

        _flashCts?.Cancel();
        _flashCts?.Dispose();
        _flashCts = new CancellationTokenSource();

        ResultOverlay.Background = correct ? FlashCorrect : FlashWrong;

        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(1200),
            FillMode = FillMode.Forward,
            Easing   = new CubicEaseOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0.00), Setters = { new Setter(OpacityProperty, 0.0) } },
                new KeyFrame { Cue = new Cue(0.08), Setters = { new Setter(OpacityProperty, 0.12) } },
                new KeyFrame { Cue = new Cue(1.00), Setters = { new Setter(OpacityProperty, 0.0) } },
            }
        };

        animation.RunAsync(ResultOverlay, _flashCts.Token);
    }

    // ── New-question fade-in ──────────────────────────────────────────────────

    private void RunContentFadeIn()
    {
        if (ContentPanel == null) return;

        _fadeCts?.Cancel();
        _fadeCts?.Dispose();
        _fadeCts = new CancellationTokenSource();

        ContentPanel.Opacity = 0;

        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(380),
            FillMode = FillMode.Forward,
            Easing   = new CubicEaseOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0.0), Setters = { new Setter(OpacityProperty, 0.0) } },
                new KeyFrame { Cue = new Cue(1.0), Setters = { new Setter(OpacityProperty, 1.0) } },
            }
        };

        animation.RunAsync(ContentPanel, _fadeCts.Token);
    }

    // ── Click handlers ───────────────────────────────────────────────────────

    private void OnPoolItemClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TriviaPoolItemVm item } &&
            DataContext is TriviaViewModel vm)
        {
            vm.SelectPoolItem(item);
        }
    }

    private void OnMcAnswerClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TriviaMcAnswerVm answer } &&
            DataContext is TriviaViewModel vm)
        {
            vm.SelectMcAnswer(answer);
        }
    }
}
