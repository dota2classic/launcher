using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

public partial class SelectGameViewModel : ViewModelBase
{
    private readonly IContentRegistryService _registryService;

    [ObservableProperty] private string? _selectedDownloadPath;
    [ObservableProperty] private string? _downloadPathError;
    [ObservableProperty] private string? _selectedInstalledPath;
    [ObservableProperty] private string? _installedPathError;
    [ObservableProperty] private bool _isChoosingDlc;
    [ObservableProperty] private bool _isDlcLoading;

    /// <summary>Packages shown in the DLC selection panel.</summary>
    public IReadOnlyList<DlcPackageItem> DlcPackages { get; private set; } = [];

    /// <summary>
    /// DLC IDs already selected in settings — used to pre-populate the selector.
    /// Null means the user has never seen the selector.
    /// </summary>
    public List<string>? ExistingDlcIds { get; set; }

    /// <summary>Called after the user confirms DLC selection with the chosen optional IDs.</summary>
    public Action<List<string>>? OnDlcSelectionSaved { get; set; }

    internal Action<string>? GameDirectorySelected { get; set; }

    private string? _pendingGameDirectory;

    public SelectGameViewModel(IContentRegistryService registryService)
    {
        _registryService = registryService;
    }

    internal void NotifyGameDirectorySelected(string path)
    {
        GameDirectorySelected?.Invoke(path);
    }

    /// <summary>
    /// Called from the view after the user picks a download folder.
    /// Fetches the registry and shows the DLC selection panel.
    /// If the registry is unavailable, proceeds directly with no optional DLC.
    /// </summary>
    internal async Task StartDlcSelectionAsync(string path)
    {
        _pendingGameDirectory = path;
        IsDlcLoading = true;

        var registry = await _registryService.GetAsync();

        IsDlcLoading = false;

        if (registry == null || !registry.Packages.Any(p => p.Optional))
        {
            // No optional content or registry unavailable — skip selector
            OnDlcSelectionSaved?.Invoke([]);
            NotifyGameDirectorySelected(path);
            return;
        }

        var items = new List<DlcPackageItem>();

        foreach (var pkg in registry.Packages)
        {
            if (!pkg.Optional)
            {
                // Required: shown checked and disabled
                items.Add(new DlcPackageItem { Id = pkg.Id, Name = pkg.Name, IsRequired = true, IsSelected = true });
            }
            else
            {
                var alreadySelected = ExistingDlcIds?.Contains(pkg.Id) ?? false;
                items.Add(new DlcPackageItem { Id = pkg.Id, Name = pkg.Name, IsSelected = alreadySelected });
            }
        }

        DlcPackages = items;
        OnPropertyChanged(nameof(DlcPackages));
        IsChoosingDlc = true;
    }

    [RelayCommand]
    private void ConfirmDlcSelection()
    {
        var selectedOptionalIds = DlcPackages
            .Where(p => p.IsEnabled && p.IsSelected)
            .Select(p => p.Id)
            .ToList();

        OnDlcSelectionSaved?.Invoke(selectedOptionalIds);
        IsChoosingDlc = false;
        NotifyGameDirectorySelected(_pendingGameDirectory!);
    }
}
