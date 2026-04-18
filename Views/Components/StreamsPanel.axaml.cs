using System;
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

    private void OnUrlButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string url }) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return;
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
