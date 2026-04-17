using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using d2c_launcher.Models;
using d2c_launcher.Services;
using d2c_launcher.Util;

namespace d2c_launcher.ViewModels;

public partial class DlcViewModel : ViewModelBase
{
    private readonly ISettingsStorage _settingsStorage;
    private readonly IContentRegistryService _registryService;

    /// <summary>
    /// Called when the user applies DLC changes. Parent VM uses this to trigger
    /// re-verification; deletion is computed from settings by GameDownloadViewModel.
    /// </summary>
    public Action? OnDlcChanged { get; set; }

    public IReadOnlyList<DlcPackageItem> DlcPackages { get; private set; } = [];

    private Dictionary<string, bool> _originalDlcSelection = new();

    [ObservableProperty] private bool _hasDlcChanges;

    public async Task LoadDlcPackagesAsync()
    {
        AppLog.Info("[DLC] LoadDlcPackagesAsync started");
        var registry = await _registryService.GetAsync();
        if (registry == null)
        {
            AppLog.Info("[DLC] Registry returned null — no packages to show");
            return;
        }
        AppLog.Info($"[DLC] Registry has {registry.Packages?.Count ?? 0} package(s)");

        var settings = _settingsStorage.Get();
        var installedIds = settings.InstalledPackageIds;
        var selectedDlcIds = settings.SelectedDlcIds ?? [];

        var items = new List<DlcPackageItem>();
        _originalDlcSelection = new Dictionary<string, bool>();

        foreach (var pkg in registry.Packages ?? [])
        {
            bool installed = installedIds != null
                ? installedIds.Contains(pkg.Id)
                : !pkg.Optional || selectedDlcIds.Contains(pkg.Id);

            var item = new DlcPackageItem
            {
                Id = pkg.Id,
                Name = pkg.Name,
                IsRequired = !pkg.Optional,
                IsSelected = installed
            };

            _originalDlcSelection[pkg.Id] = installed;

            if (pkg.Optional)
            {
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(DlcPackageItem.IsSelected))
                        UpdateHasDlcChanges();
                };
            }

            items.Add(item);
        }

        AppLog.Info($"[DLC] Built {items.Count} DlcPackageItem(s) for display");
        DlcPackages = items;
        HasDlcChanges = false;
        OnPropertyChanged(nameof(DlcPackages));
    }

    private void UpdateHasDlcChanges()
    {
        foreach (var item in DlcPackages)
        {
            if (_originalDlcSelection.TryGetValue(item.Id, out var original) && original != item.IsSelected)
            {
                HasDlcChanges = true;
                return;
            }
        }
        HasDlcChanges = false;
    }

    [RelayCommand]
    private void ApplyDlcChanges()
    {
        var removedIds = DlcPackages
            .Where(p => _originalDlcSelection.TryGetValue(p.Id, out var wasInstalled) && wasInstalled && !p.IsSelected)
            .Select(p => p.Id)
            .ToList();

        var addedIds = DlcPackages
            .Where(p => _originalDlcSelection.TryGetValue(p.Id, out var wasInstalled) && !wasInstalled && p.IsSelected)
            .Select(p => p.Id)
            .ToList();

        var settings = _settingsStorage.Get();

        // InstalledPackageIds is intentionally NOT mutated here.
        // GameDownloadViewModel.DeleteRemovedPackagesAsync diffs InstalledPackageIds
        // against SelectedDlcIds to compute what to delete; it needs the pre-change
        // installed state. OnPackagesInstalled is the sole writer after verification completes.
        settings.SelectedDlcIds ??= [];
        foreach (var id in removedIds)
            settings.SelectedDlcIds.Remove(id);
        foreach (var id in addedIds)
            if (!settings.SelectedDlcIds.Contains(id))
                settings.SelectedDlcIds.Add(id);

        _settingsStorage.Save(settings);

        foreach (var item in DlcPackages)
            _originalDlcSelection[item.Id] = item.IsSelected;
        HasDlcChanges = false;

        OnDlcChanged?.Invoke();
    }

    // ── Constructor ────────────────────────────────────────────────────────────

    public DlcViewModel(ISettingsStorage settingsStorage, IContentRegistryService registryService)
    {
        _settingsStorage = settingsStorage;
        _registryService = registryService;
    }
}
