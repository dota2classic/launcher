using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
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
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        UpdateSettingsGameDirectory();

        if (DataContext is MainLauncherViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(MainLauncherViewModel.IntroStep)
                                      or nameof(MainLauncherViewModel.IsIntroOpen))
                    UpdateSpotlight();
            };
            // Compute spotlight once layout is ready
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
            1 => LauncherHeaderControl.FindControl<Button>("PlayButton"),
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

    private void UpdateSettingsGameDirectory()
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.Settings.RefreshGameDirectory();
    }

    private async void OnSelectDotaExeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = this.GetVisualRoot() as TopLevel;
        if (topLevel?.StorageProvider == null || DataContext is not MainLauncherViewModel vm)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select dota.exe",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Executable") { Patterns = new[] { "*.exe" } },
                FilePickerFileTypes.All
            }
        });

        if (files.Count > 0)
        {
            var path = files[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
                vm.SetGameDirectory(Path.GetDirectoryName(path));
        }
    }

    private void OnCloseSettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.CloseSettings();
    }

    private void OnSettingsOverlayBackdropPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm && e.Source == sender)
            vm.CloseSettings();
    }

    private void OnSettingsPanelPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnPrimaryActionClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainLauncherViewModel vm)
            return;

        if (!vm.IsGameDirectorySet)
        {
            OnSelectDotaExeClicked(sender, e);
            return;
        }

        vm.LaunchGame();
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
