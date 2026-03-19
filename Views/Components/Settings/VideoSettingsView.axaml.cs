using System;
using Avalonia;
using Avalonia.Controls;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Views.Components.Settings;

public partial class VideoSettingsView : UserControl
{
    public VideoSettingsView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyMonitorSize();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ApplyMonitorSize();
    }

    private void ApplyMonitorSize()
    {
        var screen = TopLevel.GetTopLevel(this)?.Screens?.Primary;
        if (screen != null && DataContext is VideoSettingsViewModel vm)
        {
            var b = screen.Bounds;
            vm.SetMonitorSize((int)b.Width, (int)b.Height);
        }
    }
}
