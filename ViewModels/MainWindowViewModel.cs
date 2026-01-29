using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using ObsidianLauncher.Settings;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger _logger;
    private readonly LauncherConfig _launcherConfig;
    private readonly LauncherSettings _launcherSettings;
    private readonly InstanceManager _instanceManager;
    private readonly InstanceGroupManager _groupManager;
    private readonly HttpManager _httpManager;
    private readonly JavaManager _javaManager;
    private readonly AssetManager _assetManager;
    private readonly LibraryManager _libraryManager;
    private readonly ArgumentBuilder _argumentBuilder;
    private readonly GameLauncher _gameLauncher;

    private Instance? _selectedInstance;
    private string _statusText;
    private bool _isLaunching;
    private double _progressValue;
    private string _progressText;

    public MainWindowViewModel()
    {
        _logger = LogHelper.GetLogger<MainWindowViewModel>();
        _launcherConfig = new LauncherConfig();

        // Initialize settings
        var settingsPath = System.IO.Path.Combine(_launcherConfig.BaseDataPath, "launcher-settings.toml");
        _launcherSettings = new LauncherSettings(settingsPath);

        // Initialize services
        _httpManager = new HttpManager();
        _javaManager = new JavaManager(_launcherConfig, _httpManager);
        _assetManager = new AssetManager(_launcherConfig, _httpManager);
        _libraryManager = new LibraryManager(_launcherConfig, _httpManager);
        _instanceManager = new InstanceManager(_launcherConfig, _assetManager, _libraryManager, _httpManager);
        _groupManager = new InstanceGroupManager(_launcherConfig);
        _argumentBuilder = new ArgumentBuilder(_launcherConfig);
        _gameLauncher = new GameLauncher(_launcherConfig);

        Instances = new ObservableCollection<Instance>();
        Groups = new ObservableCollection<InstanceGroup>();

        _statusText = "Ready";
        _progressText = "";
        _progressValue = 0;

        // Initialize commands
        LaunchInstanceCommand = new RelayCommand(async () => await LaunchInstanceAsync(), () => SelectedInstance != null && !IsLaunching);
        CreateInstanceCommand = new RelayCommand(async () => await CreateInstanceAsync());
        EditInstanceCommand = new RelayCommand(async () => await EditInstanceAsync(), () => SelectedInstance != null);
        DeleteInstanceCommand = new RelayCommand(async () => await DeleteInstanceAsync(), () => SelectedInstance != null);
        RefreshInstancesCommand = new RelayCommand(async () => await LoadInstancesAsync());
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        ExitCommand = new RelayCommand(Exit);

        // Load initial data
        _ = LoadInstancesAsync();
        _ = LoadGroupsAsync();
    }

    public ObservableCollection<Instance> Instances { get; }
    public ObservableCollection<InstanceGroup> Groups { get; }

    public Instance? SelectedInstance
    {
        get => _selectedInstance;
        set
        {
            if (SetProperty(ref _selectedInstance, value))
            {
                ((RelayCommand)LaunchInstanceCommand).RaiseCanExecuteChanged();
                ((RelayCommand)EditInstanceCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteInstanceCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsLaunching
    {
        get => _isLaunching;
        set
        {
            if (SetProperty(ref _isLaunching, value))
            {
                ((RelayCommand)LaunchInstanceCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    public ICommand LaunchInstanceCommand { get; }
    public ICommand CreateInstanceCommand { get; }
    public ICommand EditInstanceCommand { get; }
    public ICommand DeleteInstanceCommand { get; }
    public ICommand RefreshInstancesCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }

    private async Task LoadInstancesAsync()
    {
        try
        {
            StatusText = "Loading instances...";
            var instances = await _instanceManager.GetAllInstancesAsync();

            Instances.Clear();
            foreach (var instance in instances.OrderBy(i => i.SortOrder).ThenBy(i => i.Name))
            {
                Instances.Add(instance);
            }

            StatusText = $"Loaded {Instances.Count} instance(s)";
            _logger.Information("Loaded {Count} instances", Instances.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load instances");
            StatusText = "Error loading instances";
        }
    }

    private async Task LoadGroupsAsync()
    {
        try
        {
            var groups = _groupManager.GetAllGroups();

            Groups.Clear();
            foreach (var group in groups)
            {
                Groups.Add(group);
            }

            _logger.Information("Loaded {Count} groups", Groups.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load groups");
        }
    }

    private async Task LaunchInstanceAsync()
    {
        if (SelectedInstance == null || IsLaunching)
            return;

        try
        {
            IsLaunching = true;
            StatusText = $"Launching {SelectedInstance.Name}...";
            ProgressValue = 0;
            ProgressText = "Preparing...";

            _logger.Information("Launching instance: {InstanceName}", SelectedInstance.Name);

            // Sync instance (download assets, libraries, etc.)
            var assetProgress = new Progress<AssetDownloadProgress>(report =>
            {
                ProgressValue = report.TotalFiles > 0 ? (double)report.ProcessedFiles / report.TotalFiles * 100 : 0;
                ProgressText = $"Assets: {report.ProcessedFiles}/{report.TotalFiles}";
            });

            var libraryProgress = new Progress<LibraryProcessingProgress>(report =>
            {
                ProgressValue = report.TotalLibraries > 0 ? (double)report.ProcessedLibraries / report.TotalLibraries * 100 : 0;
                ProgressText = $"Libraries: {report.ProcessedLibraries}/{report.TotalLibraries}";
            });

            var (success, clientJarPath, libraryJarPaths) = await _instanceManager.SyncInstanceAsync(
                SelectedInstance,
                assetProgress,
                libraryProgress
            );

            if (!success)
            {
                StatusText = "Failed to sync instance";
                _logger.Error("Failed to sync instance: {InstanceName}", SelectedInstance.Name);
                return;
            }

            // Build launch profile
            var launchProfile = await _instanceManager.BuildLaunchProfileAsync(SelectedInstance.Components, default);
            if (launchProfile == null)
            {
                StatusText = "Failed to build launch profile";
                _logger.Error("Failed to build launch profile for instance: {InstanceName}", SelectedInstance.Name);
                return;
            }

            // Ensure Java runtime
            ProgressText = "Ensuring Java runtime...";
            var javaRuntime = await _javaManager.EnsureJavaForMinecraftVersionAsync(launchProfile);
            if (javaRuntime == null)
            {
                StatusText = "Failed to get Java runtime";
                _logger.Error("Failed to get Java runtime for instance: {InstanceName}", SelectedInstance.Name);
                return;
            }

            // Build arguments
            _argumentBuilder.SetOfflinePlayerName($"Player{Random.Shared.Next(100, 999)}");
            var classpathString = _argumentBuilder.BuildClasspath(clientJarPath!, libraryJarPaths!);
            var jvmArgs = _argumentBuilder.BuildJvmArguments(launchProfile, classpathString, SelectedInstance.NativesPath, javaRuntime, SelectedInstance.InstancePath);
            var gameArgs = _argumentBuilder.BuildGameArguments(launchProfile, SelectedInstance.InstancePath);

            // Launch game
            ProgressText = "Launching game...";
            var sessionStartTime = DateTime.UtcNow;
            var exitCode = await _gameLauncher.LaunchAsync(
                javaRuntime.JavaExecutablePath,
                jvmArgs,
                launchProfile.MainClass,
                gameArgs,
                SelectedInstance.GameDataPath
            );

            var sessionDuration = DateTime.UtcNow - sessionStartTime;
            await _instanceManager.UpdateLastPlayedAsync(SelectedInstance, sessionDuration);

            StatusText = exitCode == 0 ? "Game closed successfully" : $"Game closed with exit code {exitCode}";
            ProgressValue = 0;
            ProgressText = "";

            _logger.Information("Game session completed. Exit code: {ExitCode}, Duration: {Duration}", exitCode, sessionDuration);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to launch instance");
            StatusText = "Launch failed";
            ProgressValue = 0;
            ProgressText = "";
        }
        finally
        {
            IsLaunching = false;
        }
    }

    private async Task CreateInstanceAsync()
    {
        // TODO: Show create instance dialog
        _logger.Information("Create instance requested");
        StatusText = "Create instance dialog not yet implemented";
    }

    private async Task EditInstanceAsync()
    {
        if (SelectedInstance == null)
            return;

        // TODO: Show edit instance dialog
        _logger.Information("Edit instance requested: {InstanceName}", SelectedInstance.Name);
        StatusText = "Edit instance dialog not yet implemented";
    }

    private async Task DeleteInstanceAsync()
    {
        if (SelectedInstance == null)
            return;

        // TODO: Show confirmation dialog
        try
        {
            var success = await _instanceManager.DeleteInstanceAsync(SelectedInstance.Name, createBackup: true);
            if (success)
            {
                Instances.Remove(SelectedInstance);
                SelectedInstance = null;
                StatusText = "Instance deleted successfully";
            }
            else
            {
                StatusText = "Failed to delete instance";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete instance");
            StatusText = "Error deleting instance";
        }
    }

    private async void OpenSettings()
    {
        try
        {
            _logger.Information("Opening settings dialog");

            var settingsViewModel = new SettingsViewModel(_launcherSettings);
            var settingsWindow = new Views.SettingsWindow(settingsViewModel);

            // Get the main window to show the dialog as modal
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                await settingsWindow.ShowDialog(desktop.MainWindow!);
            }

            StatusText = "Settings updated";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open settings dialog");
            StatusText = "Failed to open settings dialog";
        }
    }

    private void Exit()
    {
        System.Environment.Exit(0);
    }
}

// Simple RelayCommand implementation
public class RelayCommand : ICommand
{
    private readonly Func<Task> _executeAsync;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(() => { execute(); return Task.CompletedTask; }, canExecute)
    {
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    public async void Execute(object? parameter)
    {
        await _executeAsync();
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
