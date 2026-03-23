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
    private System.ComponentModel.PropertyChangedEventHandler? _vmPropertyChangedHandler;
    private MainLauncherViewModel? _currentVm;

    public MainLauncherView()
    {
        InitializeComponent();

        // Handle routed events from the SettingsPanel component
        AddHandler(SettingsPanel.SelectDirectoryRequestedEvent, OnSelectDotaExeClicked);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vmPropertyChangedHandler != null && _currentVm != null)
            _currentVm.Intro.PropertyChanged -= _vmPropertyChangedHandler;

        _vmPropertyChangedHandler = null;
        _currentVm = null;

        if (DataContext is MainLauncherViewModel vm)
        {
            _currentVm = vm;
            _vmPropertyChangedHandler = (_, args) =>
            {
                if (args.PropertyName is nameof(IntroViewModel.Step)
                                      or nameof(IntroViewModel.IsOpen))
                    UpdateSpotlight();
            };
            vm.Intro.PropertyChanged += _vmPropertyChangedHandler;
            vm.Settings.RefreshGameDirectory();
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

        Control? target = vm.Intro.Step switch
        {
            1 => LauncherHeaderControl.FindControl<Button>("LaunchGameButton"),
            2 => LauncherHeaderControl.FindControl<Button>("SettingsButton"),
            3 or 4 => GameSearchPanelControl,
            _ => null
        };

        if (target == null || !vm.Intro.IsOpen)
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

        try
        {
            var (dir, _) = await DotaExePicker.PickAsync(topLevel);
            if (dir != null)
                vm.SetGameDirectory(dir);
        }
        catch (Exception ex)
        {
            AppLog.Error("Ошибка выбора директории игры", ex);
        }
    }

    private void OnInvitePlayerClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainLauncherViewModel vm)
            vm.OpenInviteModal();
    }

    private async void OnSearchGameClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainLauncherViewModel vm)
            return;

        try
        {
            await vm.ToggleSearchAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error("Ошибка переключения поиска игры", ex);
        }
    }

    private async void OnInviteCandidateClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainLauncherViewModel vm)
            return;

        if (sender is not Avalonia.Controls.Button button)
            return;

        if (button.DataContext is not d2c_launcher.Models.InviteCandidateView candidate)
            return;

        try
        {
            await vm.InvitePlayerAsync(candidate);
            vm.CloseInviteModal();
        }
        catch (Exception ex)
        {
            AppLog.Error("Ошибка приглашения игрока", ex);
        }
    }
}
