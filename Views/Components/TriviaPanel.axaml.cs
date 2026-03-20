using Avalonia.Controls;
using Avalonia.Interactivity;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Views.Components;

public partial class TriviaPanel : UserControl
{
    public TriviaPanel()
    {
        InitializeComponent();
    }

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
