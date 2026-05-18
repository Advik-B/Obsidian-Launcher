using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Threading;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using Serilog;

namespace ObsidianLauncher.ViewModels;

/// <summary>
/// ViewModel for the Minecraft version selection dialog.
/// Fetches real version data from the Mojang manifest API.
/// </summary>
public class VersionSelectorViewModel : INotifyPropertyChanged
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpManager? _httpManager;
    private MinecraftVersionEntry? _selectedVersionEntry;
    private bool _showSnapshots;
    private bool _showOldAlpha;
    private bool _showOldBeta;
    private bool _showReleasesOnly;
    private bool _isLoading;
    private string _searchText = "";
    private string _statusText = "Loading versions...";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<MinecraftVersionEntry> AllVersions { get; } = new();
    public ObservableCollection<MinecraftVersionEntry> FilteredVersions { get; } = new();

    /// <summary>Selected item in the ListBox (a MinecraftVersionEntry object).</summary>
    public MinecraftVersionEntry? SelectedVersionEntry
    {
        get => _selectedVersionEntry;
        set
        {
            if (_selectedVersionEntry != value)
            {
                _selectedVersionEntry = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedVersion));
                ((RelayCommand)SelectCommand).RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Selected version ID (string), derived from SelectedVersionEntry.</summary>
    public string? SelectedVersion => _selectedVersionEntry?.Id;

    public bool ShowSnapshots
    {
        get => _showSnapshots;
        set { if (_showSnapshots != value) { _showSnapshots = value; OnPropertyChanged(); ApplyFilter(); } }
    }

    public bool ShowOldAlpha
    {
        get => _showOldAlpha;
        set { if (_showOldAlpha != value) { _showOldAlpha = value; OnPropertyChanged(); ApplyFilter(); } }
    }

    public bool ShowOldBeta
    {
        get => _showOldBeta;
        set { if (_showOldBeta != value) { _showOldBeta = value; OnPropertyChanged(); ApplyFilter(); } }
    }

    public bool ShowReleasesOnly
    {
        get => _showReleasesOnly;
        set { if (_showReleasesOnly != value) { _showReleasesOnly = value; OnPropertyChanged(); ApplyFilter(); } }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(); } }
    }

    public string SearchText
    {
        get => _searchText;
        set { if (_searchText != value) { _searchText = value; OnPropertyChanged(); ApplyFilter(); } }
    }

    public string StatusText
    {
        get => _statusText;
        set { if (_statusText != value) { _statusText = value; OnPropertyChanged(); } }
    }

    public ICommand SelectCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RefreshCommand { get; }

    public event EventHandler? SelectRequested;

    public VersionSelectorViewModel() : this(showSnapshots: true, showOldAlpha: false, showOldBeta: false) { }

    public VersionSelectorViewModel(bool showSnapshots, bool showOldAlpha, bool showOldBeta)
    {
        _showSnapshots = showSnapshots;
        _showOldAlpha = showOldAlpha;
        _showOldBeta = showOldBeta;

        SelectCommand = new RelayCommand(
            () => SelectRequested?.Invoke(this, EventArgs.Empty),
            () => SelectedVersionEntry != null);
        CancelCommand = new RelayCommand(() => { });
        RefreshCommand = new RelayCommand(async () => await LoadVersionsAsync());

        _ = LoadVersionsAsync();
    }

    public VersionSelectorViewModel(HttpManager httpManager, bool showSnapshots = true, bool showOldAlpha = false, bool showOldBeta = false)
        : this(showSnapshots, showOldAlpha, showOldBeta)
    {
        _httpManager = httpManager;
    }

    private async System.Threading.Tasks.Task LoadVersionsAsync()
    {
        IsLoading = true;
        StatusText = "Fetching version list...";

        try
        {
            using var fallbackHttp = _httpManager == null ? new HttpManager() : null;
            var http = _httpManager ?? fallbackHttp!;
            var response = await http.GetAsync("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json");

            if (!response.IsSuccessStatusCode)
            {
                StatusText = $"Failed to fetch versions: {response.StatusCode}";
                Log.Warning("VersionSelector: manifest fetch failed: {Status}", response.StatusCode);
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var manifest = JsonSerializer.Deserialize<VersionManifest>(json, JsonOptions);

            if (manifest?.Versions == null)
            {
                StatusText = "Failed to parse version manifest";
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                AllVersions.Clear();
                foreach (var v in manifest.Versions)
                {
                    AllVersions.Add(new MinecraftVersionEntry
                    {
                        Id = v.Id,
                        Type = v.Type,
                        ReleaseTime = v.ReleaseTime
                    });
                }

                ApplyFilter();
                StatusText = $"{manifest.Versions.Count} versions available";
                Log.Information("VersionSelector: loaded {Count} versions", manifest.Versions.Count);
            });
        }
        catch (Exception ex)
        {
            StatusText = "Error loading versions";
            Log.Error(ex, "VersionSelector: exception loading versions");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredVersions.Clear();

        var filtered = AllVersions.AsEnumerable();

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

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLowerInvariant();
            filtered = filtered.Where(v => v.Id.ToLowerInvariant().Contains(search));
        }

        foreach (var version in filtered.OrderByDescending(v => v.ReleaseTime))
            FilteredVersions.Add(version);

        Log.Debug("Filtered to {Count} versions", FilteredVersions.Count);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Represents a single Minecraft version entry shown in the version selector.
/// </summary>
public class MinecraftVersionEntry
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
