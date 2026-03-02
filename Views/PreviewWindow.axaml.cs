using Avalonia.Controls;
using d2c_launcher.Preview;

namespace d2c_launcher.Views;

public partial class PreviewWindow : Window
{
    public PreviewWindow() { InitializeComponent(); }

    public PreviewWindow(string componentName)
    {
        InitializeComponent();
        Title = $"Preview: {componentName}";

        var (view, viewModel) = PreviewRegistry.Create(componentName);
        if (viewModel != null)
            view.DataContext = viewModel;

        Host.Content = view;
    }
}
