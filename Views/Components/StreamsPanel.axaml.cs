using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace d2c_launcher.Views.Components;

public partial class StreamsPanel : UserControl
{
    public StreamsPanel()
    {
        InitializeComponent();
    }

    private void OnStreamCardClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url } && !string.IsNullOrEmpty(url))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
