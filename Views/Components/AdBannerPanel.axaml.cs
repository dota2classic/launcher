using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace d2c_launcher.Views.Components;

public partial class AdBannerPanel : UserControl
{
    public AdBannerPanel()
    {
        InitializeComponent();
    }

    private void OnBannerClicked(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://collectorshop.ru") { UseShellExecute = true });
    }
}
