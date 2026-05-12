// ViewModels/VersionSelectorViewModel.cs

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

public class VersionSelectorViewModel : INotifyPropertyChanged
{
    private readonly HttpManager? _httpManager;
    private MinecraftVersionEntry? _selectedVersion;
    private bool _showSnapshots = true;
    private bool _showOldAlpha = false;
    private bool _showOldBeta = false;
    private bool _showReleasesOnly = false;
    private string _searchText = "";
    private bool _isLoading;
    private string _loadError = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<MinecraftVersionEntry> AllVersions { get; }
    public ObservableCollection<MinecraftVersionEntry> FilteredVersions { get; }

    public MinecraftVersionEntry? SelectedVersion
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

    public string SearchText
    {
        get => _searchText;
        set { if (_searchText != value) { _searchText = value; OnPropertyChanged(); ApplyFilter(); } }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(); } }
    }

    public string LoadError
    {
        get => _loadError;
        private set { if (_loadError != value) { _loadError = value; OnPropertyChanged(); } }
    }

    public ICommand SelectCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RefreshCommand { get; }

    // Designer constructor — shows a few placeholder entries
    public VersionSelectorViewModel()
    {
        AllVersions = new ObservableCollection<MinecraftVersionEntry>();
        FilteredVersions = new ObservableCollection<MinecraftVersionEntry>();
        SelectCommand = new RelayCommand(() => { }, () => SelectedVersion != null);
        CancelCommand = new RelayCommand(() => { });
        RefreshCommand = new RelayCommand(() => { });

        AllVersions.Add(new MinecraftVersionEntry { Id = "1.21.4", Type = "release", ReleaseTime = DateTime.Now });
        AllVersions.Add(new MinecraftVersionEntry { Id = "25w07a", Type = "snapshot", ReleaseTime = DateTime.Now.AddDays(-7) });
        ApplyFilter();
    }

    public VersionSelectorViewModel(HttpManager httpManager) : this()
    {
        _httpManager = httpManager;
        AllVersions.Clear();
        FilteredVersions.Clear();
        RefreshCommand.Execute(null);
        _ = LoadVersionsAsync();
    }

    private async System.Threading.Tasks.Task LoadVersionsAsync()
    {
        if (_httpManager == null) return;

        Dispatcher.UIThread.Post(() => { IsLoading = true; LoadError = ""; });

        try
        {
            var response = await _httpManager.GetAsync(
                "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json");

            if (!response.IsSuccessStatusCode)
            {
                Dispatcher.UIThread.Post(() =>
                    LoadError = $"Failed to fetch version list (HTTP {(int)response.StatusCode})");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var manifest = JsonSerializer.Deserialize<VersionManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (manifest == null)
            {
                Dispatcher.UIThread.Post(() => LoadError = "Failed to parse version manifest.");
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
                Log.Information("Loaded {Count} Minecraft versions from manifest", AllVersions.Count);
                ApplyFilter();
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching Minecraft version manifest");
            Dispatcher.UIThread.Post(() => LoadError = $"Error: {ex.Message}");
        }
        finally
        {
            Dispatcher.UIThread.Post(() => IsLoading = false);
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
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

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
