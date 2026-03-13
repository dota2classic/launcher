using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using d2c_launcher.ViewModels;
using System;

namespace d2c_launcher.Views;

public partial class LaunchSteamFirstView : UserControl
{
    private static readonly Animation PulseAnimation = new()
    {
        Duration = TimeSpan.FromMilliseconds(1500),
        Easing = new CubicEaseOut(),
        Children =
        {
            new KeyFrame { Cue = new Cue(0.0), Setters = { new Setter(OpacityProperty, 0.35) } },
            new KeyFrame { Cue = new Cue(1.0), Setters = { new Setter(OpacityProperty, 0.0) } },
        }
    };

    public LaunchSteamFirstView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is LaunchSteamFirstViewModel vm)
                vm.CheckOccurred += OnCheckOccurred;
        };
    }

    private void OnCheckOccurred()
    {
        Dispatcher.UIThread.Post(() => _ = PulseAnimation.RunAsync(PulseRing));
    }
}
