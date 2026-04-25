using Avalonia.Controls;
using Avalonia.Interactivity;
using d2c_launcher.ViewModels;

namespace d2c_launcher.Views.Components;

public partial class ProfilePanel : UserControl
{
    public ProfilePanel()
    {
        InitializeComponent();
    }

    private void OnGeneralTabTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProfileViewModel vm)
            vm.SelectGeneralTabCommand.Execute(null);
    }

    private void OnSubscriptionTabTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProfileViewModel vm)
            vm.SelectSubscriptionTabCommand.Execute(null);
    }
}
