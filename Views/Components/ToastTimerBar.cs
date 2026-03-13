using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Views.Components;

/// <summary>
/// A thin horizontal bar that animates smoothly from full width to zero
/// using Avalonia's compositor animation — driven entirely by DisplaySeconds
/// from the nearest NotificationViewModel in the DataContext.
/// </summary>
public class ToastTimerBar : TemplatedControl
{
    static ToastTimerBar()
    {
        BackgroundProperty.OverrideDefaultValue<ToastTimerBar>(Brushes.Transparent);
        HeightProperty.OverrideDefaultValue<ToastTimerBar>(3);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is not NotificationViewModel vm)
            return;

        var fill = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#4DA9F3")),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };

        // Use a ScaleTransform anchored to the left so shrinking happens right-to-left
        var scale = new ScaleTransform(1.0, 1.0);
        fill.RenderTransformOrigin = RelativePoint.TopLeft;
        fill.RenderTransform = scale;

        // Fill the full container width via a 1x1 grid trick
        var grid = new Grid();
        grid.Children.Add(new Border { Background = new SolidColorBrush(Color.Parse("#1a2a38")) });
        grid.Children.Add(fill);

        // Keep fill width in sync with the container
        grid.SizeChanged += (_, e) => fill.Width = e.NewSize.Width;

        ((ISetLogicalParent)grid).SetParent(this);
        VisualChildren.Add(grid);
        LogicalChildren.Add(grid);

        // Kick off the Avalonia animation — runs at display refresh rate on the compositor
        var animation = new Animation
        {
            Duration = TimeSpan.FromSeconds(vm.DisplaySeconds),
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

        animation.RunAsync(fill);
    }
}
