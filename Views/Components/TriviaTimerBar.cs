using System;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Views.Components;

/// <summary>
/// A thin horizontal bar that smoothly decays from full width to zero using
/// a compositor ScaleTransform animation. Restarts automatically whenever
/// <see cref="TriviaViewModel.QuestionStarted"/> fires.
/// </summary>
public class TriviaTimerBar : TemplatedControl
{
    private TriviaViewModel? _vm;
    private CancellationTokenSource? _cts;
    private Border? _fill;
    private ScaleTransform? _scale;
    private bool _visualBuilt;

    static TriviaTimerBar()
    {
        BackgroundProperty.OverrideDefaultValue<TriviaTimerBar>(Brushes.Transparent);
        HeightProperty.OverrideDefaultValue<TriviaTimerBar>(4);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        BuildVisual();
        Subscribe(DataContext as TriviaViewModel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unsubscribe();
        _cts?.Cancel();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Unsubscribe();
        Subscribe(DataContext as TriviaViewModel);
    }

    private void BuildVisual()
    {
        if (_visualBuilt) return;
        _visualBuilt = true;

        _scale = new ScaleTransform(1.0, 1.0);
        _fill = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#C8A84B")),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            RenderTransformOrigin = RelativePoint.TopLeft,
            RenderTransform = _scale,
        };

        var track = new Border { Background = new SolidColorBrush(Color.Parse("#2d3842")) };
        var grid = new Grid();
        grid.Children.Add(track);
        grid.Children.Add(_fill);
        grid.SizeChanged += (_, args) => _fill.Width = args.NewSize.Width;

        ((ISetLogicalParent)grid).SetParent(this);
        VisualChildren.Add(grid);
        LogicalChildren.Add(grid);
    }

    private void Subscribe(TriviaViewModel? vm)
    {
        if (vm == null) return;
        _vm = vm;
        vm.QuestionStarted += OnQuestionStarted;

        // If a question is already running (bar attached mid-question), animate remaining time.
        if (vm.TimerSeconds > 0)
            StartAnimation(vm.TimerSeconds);
    }

    private void Unsubscribe()
    {
        if (_vm == null) return;
        _vm.QuestionStarted -= OnQuestionStarted;
        _vm = null;
    }

    private void OnQuestionStarted(int durationSeconds) => StartAnimation(durationSeconds);

    private void StartAnimation(int durationSeconds)
    {
        if (_fill == null || _scale == null) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _scale.ScaleX = 1.0;

        var animation = new Animation
        {
            Duration = TimeSpan.FromSeconds(durationSeconds),
            FillMode = FillMode.Forward,
            Easing = new LinearEasing(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0.0),
                    Setters = { new Setter(ScaleTransform.ScaleXProperty, 1.0) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1.0),
                    Setters = { new Setter(ScaleTransform.ScaleXProperty, 0.0) }
                }
            }
        };

        animation.RunAsync(_fill, _cts.Token);
    }
}
