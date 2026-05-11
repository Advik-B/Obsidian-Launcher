// ViewModels/VersionSelectorViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Serilog;

namespace ObsidianLauncher.ViewModels;

/// <summary>
///     ViewModel for the Minecraft Version Selection dialog.
/// </summary>
public class VersionSelectorViewModel : INotifyPropertyChanged
{
    private string? _selectedVersion;
    private bool _showSnapshots = true;
    private bool _showOldAlpha = false;
    private bool _showOldBeta = false;
    private bool _showReleasesOnly = false;
    private string _searchText = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     All available Minecraft versions.
    /// </summary>
    public ObservableCollection<MinecraftVersion> AllVersions { get; }

    /// <summary>
    ///     Filtered versions based on search and filter settings.
    /// </summary>
    public ObservableCollection<MinecraftVersion> FilteredVersions { get; }

    /// <summary>
    ///     Selected Minecraft version ID.
    /// </summary>
    public string? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (_selectedVersion != value)
            {
                _selectedVersion = value;
                OnPropertyChanged();
                ((RelayCommand)SelectCommand).RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    ///     Show snapshot versions.
    /// </summary>
    public bool ShowSnapshots
    {
        get => _showSnapshots;
        set
        {
            if (_showSnapshots != value)
            {
                _showSnapshots = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }

    /// <summary>
    ///     Show old alpha versions.
    /// </summary>
    public bool ShowOldAlpha
    {
        get => _showOldAlpha;
        set
        {
            if (_showOldAlpha != value)
            {
                _showOldAlpha = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }

    /// <summary>
    ///     Show old beta versions.
    /// </summary>
    public bool ShowOldBeta
    {
        get => _showOldBeta;
        set
        {
            if (_showOldBeta != value)
            {
                _showOldBeta = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }

    /// <summary>
    ///     Show release versions only.
    /// </summary>
    public bool ShowReleasesOnly
    {
        get => _showReleasesOnly;
        set
        {
            if (_showReleasesOnly != value)
            {
                _showReleasesOnly = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }

    /// <summary>
    ///     Search/filter text.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }

    // Commands
    public ICommand SelectCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RefreshCommand { get; }

    public VersionSelectorViewModel() : this(showSnapshots: true, showOldAlpha: false, showOldBeta: false) { }

    public VersionSelectorViewModel(bool showSnapshots, bool showOldAlpha, bool showOldBeta)
    {
        _showSnapshots = showSnapshots;
        _showOldAlpha = showOldAlpha;
        _showOldBeta = showOldBeta;

        AllVersions = new ObservableCollection<MinecraftVersion>();
        FilteredVersions = new ObservableCollection<MinecraftVersion>();

        // Initialize commands
        SelectCommand = new RelayCommand(() => { }, () => SelectedVersion != null); // Dialog handles close with result
        CancelCommand = new RelayCommand(() => { }); // Dialog handles close
        RefreshCommand = new RelayCommand(RefreshVersions);

        // Load versions
        LoadVersions();
    }

    private void LoadVersions()
    {
        // TODO: Load from Minecraft version manifest
        // For now, add some sample versions
        AllVersions.Add(new MinecraftVersion { Id = "1.20.4", Type = "release", ReleaseTime = DateTime.Now });
        AllVersions.Add(new MinecraftVersion { Id = "1.20.3", Type = "release", ReleaseTime = DateTime.Now.AddDays(-30) });
        AllVersions.Add(new MinecraftVersion { Id = "24w06a", Type = "snapshot", ReleaseTime = DateTime.Now.AddDays(-7) });
        AllVersions.Add(new MinecraftVersion { Id = "1.19.4", Type = "release", ReleaseTime = DateTime.Now.AddMonths(-6) });
        AllVersions.Add(new MinecraftVersion { Id = "1.18.2", Type = "release", ReleaseTime = DateTime.Now.AddMonths(-12) });
        AllVersions.Add(new MinecraftVersion { Id = "b1.7.3", Type = "old_beta", ReleaseTime = DateTime.Now.AddYears(-10) });
        AllVersions.Add(new MinecraftVersion { Id = "a1.2.6", Type = "old_alpha", ReleaseTime = DateTime.Now.AddYears(-12) });

        Log.Information("Loaded {Count} Minecraft versions", AllVersions.Count);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredVersions.Clear();

        var filtered = AllVersions.AsEnumerable();

        // Apply type filters
        if (ShowReleasesOnly)
        {
            filtered = filtered.Where(v => v.Type == "release");
        }
        else
        {
            if (!ShowSnapshots)
                filtered = filtered.Where(v => v.Type != "snapshot");

            if (!ShowOldAlpha)
                filtered = filtered.Where(v => v.Type != "old_alpha");

            if (!ShowOldBeta)
                filtered = filtered.Where(v => v.Type != "old_beta");
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLowerInvariant();
            filtered = filtered.Where(v => v.Id.ToLowerInvariant().Contains(search));
        }

        foreach (var version in filtered.OrderByDescending(v => v.ReleaseTime))
        {
            FilteredVersions.Add(version);
        }

        Log.Information("Filtered to {Count} versions", FilteredVersions.Count);
    }

    private void RefreshVersions()
    {
        Log.Information("Refreshing version list...");
        // TODO: Re-download version manifest
        AllVersions.Clear();
        LoadVersions();
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
///     Represents a Minecraft version.
/// </summary>
public class MinecraftVersion
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "release";
    public DateTime ReleaseTime { get; set; }

    public string TypeDisplay => Type switch
    {
        "release" => "Release",
        "snapshot" => "Snapshot",
        "old_beta" => "Beta",
        "old_alpha" => "Alpha",
        _ => Type
    };
}
