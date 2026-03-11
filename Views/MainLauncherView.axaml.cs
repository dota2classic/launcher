using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using d2c_launcher.Util;
using d2c_launcher.ViewModels;
using d2c_launcher.Views.Components;

namespace d2c_launcher.Views;

public partial class MainLauncherView : UserControl
{
    public MainLauncherView()
    {
        InitializeComponent();

        // Handle routed events from the SettingsPanel component
        AddHandler(SettingsPanel.SelectDirectoryRequestedEvent, OnSelectDotaExeClicked);
        AddHandler(SettingsPanel.CloseRequestedEvent, OnCloseSettingsClicked);

        // Handle close from invite modal's ModalHeader
        InviteModalPanel.AddHandler(ModalHeader.CloseRequestedEvent, OnCloseInviteModal);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainLauncherViewModel vm)
        {
            vm.Settings.RefreshGameDirectory();
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(MainLauncherViewModel.IntroStep)
                                      or nameof(MainLauncherViewModel.IsIntroOpen))
                    UpdateSpotlight();
            };
            LayoutUpdated += OnFirstLayoutForSpotlight;
        }
    }

    private void OnFirstLayoutForSpotlight(object? sender, EventArgs e)
    {
        LayoutUpdated -= OnFirstLayoutForSpotlight;
        UpdateSpotlight();
    }

    private void UpdateSpotlight()
    {
        if (DataContext is not MainLauncherViewModel vm) return;

        Control? target = vm.IntroStep switch
        {
            // Steps 1 (play tab) and 2 (settings tab) now highlight the tab buttons in the header
            1 => LauncherHeaderControl.FindControl<Button>("PlayTabButton"),
            2 => LauncherHeaderControl.FindControl<Button>("SettingsButton"),
            3 or 4 => GameSearchPanelControl,
            _ => null
        };

        if (target == null || !vm.IsIntroOpen)
        {
            IntroSpotlight.SpotlightRect = default;
            return;
        }

        var pos = target.TranslatePoint(new Point(0, 0), this);
        IntroSpotlight.SpotlightRect = pos.HasValue
            ? new Rect(pos.Value, target.Bounds.Size)
            : default;
    }

    private async void OnSelectDotaExeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainLauncherViewModel vm)
            return;

        var topLevel = this.GetVisualRoot() as TopLevel;
        if (topLevel?.StorageProvider == null)
            return;

        var (dir, _) = await DotaExePicker.PickAsync(topLevel);
        if (dir != null)
            vm.SetGameDirectory(dir);
    }

    private void OnCloseSettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.CloseSettings();
    }

    private void OnCloseSettingsModal(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm && vm.IsSettingsOpen)
            vm.NavigateTo(LauncherTab.Settings);
    }

    private void OnSettingsOverlayBackdropPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm && e.Source == sender && vm.IsSettingsOpen)
            vm.NavigateTo(LauncherTab.Settings);
    }

    private void OnSettingsModalPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnInvitePlayerClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.OpenInviteModal();
    }

    private void OnCloseInviteModal(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.CloseInviteModal();
    }

    private void OnInviteOverlayBackdropPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm && e.Source == sender)
            vm.CloseInviteModal();
    }

    private void OnInviteModalPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private async void OnSearchGameClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            await vm.ToggleSearchAsync();
    }

    private async void OnInviteCandidateClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainLauncherViewModel vm)
            return;

        if (sender is not Avalonia.Controls.Button button)
            return;

        if (button.DataContext is not d2c_launcher.Models.InviteCandidateView candidate)
            return;

        await vm.InvitePlayerAsync(candidate.SteamId);
        vm.CloseInviteModal();
    }
}
