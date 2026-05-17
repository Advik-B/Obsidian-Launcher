using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ObsidianLauncher.Models;
using ObsidianLauncher.Models.Modrinth;
using ObsidianLauncher.Services;
using ObsidianLauncher.Services.Modrinth;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.ViewModels;

/// <summary>
/// ViewModel for the Modrinth mod browser window.
/// Allows browsing, searching, and installing mods into a specific instance.
/// </summary>
public class ModBrowserViewModel : INotifyPropertyChanged
{
    private readonly ModManager _modManager;
    private readonly Instance _instance;
    private readonly ILogger _logger;

    private string _searchQuery = "";
    private string _selectedProjectType = "mod";
    private string _statusText = "Ready";
    private bool _isLoading;
    private ModrinthProject? _selectedProject;
    private ModrinthVersion? _selectedVersion;
    private CancellationTokenSource? _searchCts;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ModrinthProject> SearchResults { get; } = new();
    public ObservableCollection<ModrinthVersion> ProjectVersions { get; } = new();
    public ObservableCollection<InstalledMod> InstalledMods { get; } = new();

    public static readonly string[] ProjectTypes = { "mod", "modpack", "resourcepack", "shader" };

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged();
                _ = SearchDebounceAsync();
            }
        }
    }

    public string SelectedProjectType
    {
        get => _selectedProjectType;
        set
        {
            if (_selectedProjectType != value)
            {
                _selectedProjectType = value;
                OnPropertyChanged();
                _ = SearchAsync();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(); } }
    }

    public string StatusText
    {
        get => _statusText;
        set { if (_statusText != value) { _statusText = value; OnPropertyChanged(); } }
    }

    public ModrinthProject? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (_selectedProject != value)
            {
                _selectedProject = value;
                OnPropertyChanged();
                ((RelayCommand)InstallCommand).RaiseCanExecuteChanged();
                if (value != null) _ = LoadProjectVersionsAsync(value);
            }
        }
    }

    public ModrinthVersion? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (_selectedVersion != value)
            {
                _selectedVersion = value;
                OnPropertyChanged();
                ((RelayCommand)InstallCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string InstanceName => _instance.Name;
    public string GameVersion => _instance.Components
        .FirstOrDefault(c => c.Uid == "net.minecraft")?.Version ?? "unknown";

    public ICommand SearchCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand RefreshInstalledCommand { get; }
    public ICommand RemoveModCommand { get; }
    public ICommand ToggleModCommand { get; }

    public ModBrowserViewModel(ModManager modManager, Instance instance)
    {
        _modManager = modManager ?? throw new ArgumentNullException(nameof(modManager));
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _logger = LogHelper.GetLogger<ModBrowserViewModel>();

        SearchCommand = new RelayCommand(async () => await SearchAsync());
        InstallCommand = new RelayCommand(async () => await InstallSelectedAsync(),
            () => SelectedVersion != null && !IsLoading);
        RefreshInstalledCommand = new RelayCommand(RefreshInstalledMods);
        RemoveModCommand = new RelayCommand<string>(RemoveMod);
        ToggleModCommand = new RelayCommand<string>(ToggleMod);

        RefreshInstalledMods();
        _ = SearchAsync();
    }

    private async Task SearchDebounceAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay(400, token);
            if (!token.IsCancellationRequested)
                await SearchAsync();
        }
        catch (OperationCanceledException) { }
    }

    private async Task SearchAsync()
    {
        IsLoading = true;
        StatusText = "Searching...";

        try
        {
            var result = await _modManager.SearchModsAsync(
                _searchQuery,
                GameVersion,
                loader: null,
                limit: 20);

            SearchResults.Clear();
            if (result?.Hits != null)
            {
                foreach (var hit in result.Hits)
                    SearchResults.Add(hit);
                StatusText = $"{result.TotalHits} results";
            }
            else
            {
                StatusText = "No results";
            }
        }
        catch (Exception ex)
        {
            StatusText = "Search failed";
            _logger.Error(ex, "Modrinth search failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadProjectVersionsAsync(ModrinthProject project)
    {
        ProjectVersions.Clear();
        SelectedVersion = null;
        StatusText = $"Loading versions for {project.Title}...";

        try
        {
            var versions = await _modManager.GetCompatibleVersionsAsync(
                project.ProjectId,
                GameVersion,
                loader: null);

            if (versions != null)
            {
                foreach (var v in versions.Take(20))
                    ProjectVersions.Add(v);

                if (ProjectVersions.Count > 0)
                    SelectedVersion = ProjectVersions[0];

                StatusText = $"{versions.Count} compatible versions";
            }
            else
            {
                StatusText = "No compatible versions found";
            }
        }
        catch (Exception ex)
        {
            StatusText = "Failed to load versions";
            _logger.Error(ex, "Failed to load versions for {ProjectId}", project.ProjectId);
        }
    }

    private async Task InstallSelectedAsync()
    {
        if (SelectedVersion == null) return;

        IsLoading = true;
        StatusText = $"Installing {SelectedProject?.Title}...";

        try
        {
            var progress = new Progress<float>(p => StatusText = $"Downloading... {p:P0}");
            var result = await _modManager.InstallModAsync(_instance, SelectedVersion, progress);

            if (result != null)
            {
                StatusText = $"Installed {SelectedProject?.Title} successfully";
                RefreshInstalledMods();
            }
            else
            {
                StatusText = "Installation failed";
            }
        }
        catch (Exception ex)
        {
            StatusText = "Installation failed";
            _logger.Error(ex, "Failed to install mod");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshInstalledMods()
    {
        InstalledMods.Clear();
        foreach (var mod in _modManager.GetInstalledMods(_instance))
            InstalledMods.Add(mod);
    }

    private void RemoveMod(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return;
        if (_modManager.RemoveMod(_instance, fileName))
        {
            RefreshInstalledMods();
            StatusText = $"Removed {fileName}";
        }
    }

    private void ToggleMod(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return;
        if (_modManager.ToggleMod(_instance, fileName))
        {
            RefreshInstalledMods();
            StatusText = $"Toggled {fileName}";
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Generic relay command that accepts a parameter.
/// </summary>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
    public void Execute(object? parameter) => _execute((T?)parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
