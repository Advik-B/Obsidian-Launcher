using System;
using System.Collections.Generic;
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
    private readonly ModLoaderService _modLoaderService;
    private readonly BackupManager _backupManager;

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
        _libraryManager = new LibraryManager(_launcherConfig, _httpManager, _assetManager);
        _instanceManager = new InstanceManager(_launcherConfig, _assetManager, _libraryManager, _httpManager);
        _groupManager = new InstanceGroupManager(_launcherConfig);
        _argumentBuilder = new ArgumentBuilder(_launcherConfig);
        _gameLauncher = new GameLauncher(_launcherConfig);
        _modLoaderService = new ModLoaderService(_httpManager);
        _backupManager = new BackupManager(_launcherConfig);

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
        OpenAccountManagementCommand = new RelayCommand(OpenAccountManagement);
        OpenLogViewerCommand = new RelayCommand(OpenLogViewer);
        OpenScreenshotViewerCommand = new RelayCommand(OpenScreenshotViewer);
        ExitCommand = new RelayCommand(Exit);
        ImportMultiMcCommand = new RelayCommand(async () => await ImportMultiMcAsync());
        CreateBackupCommand = new RelayCommand(async () => await CreateBackupAsync(), () => SelectedInstance != null);
        OpenJavaManagerCommand = new RelayCommand(OpenJavaManager);

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
                ((RelayCommand)CreateBackupCommand).RaiseCanExecuteChanged();
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
    public ICommand OpenAccountManagementCommand { get; }
    public ICommand OpenLogViewerCommand { get; }
    public ICommand OpenScreenshotViewerCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand ImportMultiMcCommand { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand OpenJavaManagerCommand { get; }

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

        ConsoleViewModel? consoleViewModel = null;
        Views.ConsoleWindow? consoleWindow = null;
        EventHandler<string>? outputHandler = null;
        var capturedLines = new List<string>();

        try
        {
            IsLaunching = true;
            StatusText = $"Launching {SelectedInstance.Name}...";
            ProgressValue = 0;
            ProgressText = "Preparing...";

            _logger.Information("Launching instance: {InstanceName}", SelectedInstance.Name);

            // Create and show console window
            consoleViewModel = new ConsoleViewModel();
            consoleWindow = new Views.ConsoleWindow(consoleViewModel);
            consoleWindow.Show();
            consoleViewModel.AddLogEntry($"Launching {SelectedInstance.Name}...", "INFO");

            // Sync instance (download assets, libraries, etc.)
            var assetProgress = new Progress<AssetDownloadProgress>(report =>
            {
                ProgressValue = report.TotalFiles > 0 ? (double)report.ProcessedFiles / report.TotalFiles * 100 : 0;
                ProgressText = $"Assets: {report.ProcessedFiles}/{report.TotalFiles}";
                consoleViewModel?.AddLogEntry($"Downloading assets: {report.ProcessedFiles}/{report.TotalFiles}", "INFO");
            });

            var libraryProgress = new Progress<LibraryProcessingProgress>(report =>
            {
                ProgressValue = report.TotalLibraries > 0 ? (double)report.ProcessedLibraries / report.TotalLibraries * 100 : 0;
                ProgressText = $"Libraries: {report.ProcessedLibraries}/{report.TotalLibraries}";
                consoleViewModel?.AddLogEntry($"Processing libraries: {report.ProcessedLibraries}/{report.TotalLibraries}", "INFO");
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
                consoleViewModel?.AddLogEntry("Failed to sync instance", "ERROR");
                return;
            }

            // Build launch profile
            consoleViewModel?.AddLogEntry("Building launch profile...", "INFO");
            var launchProfile = await _instanceManager.BuildLaunchProfileAsync(SelectedInstance.Components, default);
            if (launchProfile == null)
            {
                StatusText = "Failed to build launch profile";
                _logger.Error("Failed to build launch profile for instance: {InstanceName}", SelectedInstance.Name);
                consoleViewModel?.AddLogEntry("Failed to build launch profile", "ERROR");
                return;
            }

            // Ensure Java runtime
            ProgressText = "Ensuring Java runtime...";
            consoleViewModel?.AddLogEntry("Checking Java runtime...", "INFO");
            var javaRuntime = await _javaManager.EnsureJavaForMinecraftVersionAsync(launchProfile);
            if (javaRuntime == null)
            {
                StatusText = "Failed to get Java runtime";
                _logger.Error("Failed to get Java runtime for instance: {InstanceName}", SelectedInstance.Name);
                consoleViewModel?.AddLogEntry("Failed to get Java runtime", "ERROR");
                return;
            }

            consoleViewModel?.AddLogEntry($"Using Java: {javaRuntime.JavaExecutablePath}", "INFO");

            // Build arguments
            _argumentBuilder.SetOfflinePlayerName($"Player{Random.Shared.Next(100, 999)}");

            // Wire quickplay if configured
            if (!string.IsNullOrWhiteSpace(SelectedInstance.QuickPlayServer))
                _argumentBuilder.SetQuickPlayMultiplayer(SelectedInstance.QuickPlayServer);
            else if (!string.IsNullOrWhiteSpace(SelectedInstance.QuickPlayWorld))
                _argumentBuilder.SetQuickPlaySingleplayer(SelectedInstance.QuickPlayWorld);

            var classpathString = _argumentBuilder.BuildClasspath(clientJarPath!, libraryJarPaths!);
            var jvmArgs = _argumentBuilder.BuildJvmArguments(launchProfile, classpathString, SelectedInstance.NativesPath, javaRuntime, SelectedInstance.InstancePath);
            var gameArgs = _argumentBuilder.BuildGameArguments(launchProfile, SelectedInstance.InstancePath);

            // Run pre-launch command
            if (!string.IsNullOrWhiteSpace(SelectedInstance.PreLaunchCommand))
            {
                consoleViewModel?.AddLogEntry($"Running pre-launch command: {SelectedInstance.PreLaunchCommand}", "INFO");
                await RunShellCommandAsync(SelectedInstance.PreLaunchCommand, SelectedInstance.GameDataPath);
            }

            // Launch game
            ProgressText = "Launching game...";
            consoleViewModel?.AddLogEntry("Starting Minecraft...", "INFO");
            consoleViewModel?.AddLogEntry($"Main class: {launchProfile.MainClass}", "DEBUG");

            var sessionStartTime = DateTime.UtcNow;

            // Capture game output to console
            outputHandler = (sender, line) =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    capturedLines.Add(line);
                    var logLevel = "INFO";
                    if (line.Contains("[ERROR]") || line.Contains("ERROR")) logLevel = "ERROR";
                    else if (line.Contains("[WARN]") || line.Contains("WARN")) logLevel = "WARN";
                    else if (line.Contains("[DEBUG]") || line.Contains("DEBUG")) logLevel = "DEBUG";

                    consoleViewModel?.AddLogEntry(line, logLevel);
                }
            };

            _gameLauncher.OutputReceived += outputHandler;

            var envVars = SelectedInstance.EnvironmentVariables.Count > 0 ? SelectedInstance.EnvironmentVariables : null;
            var exitCode = await _gameLauncher.LaunchAsync(
                javaRuntime.JavaExecutablePath,
                jvmArgs,
                launchProfile.MainClass,
                gameArgs,
                SelectedInstance.GameDataPath,
                envVars,
                SelectedInstance.WrapperCommand
            );

            var sessionDuration = DateTime.UtcNow - sessionStartTime;
            await _instanceManager.UpdateLastPlayedAsync(SelectedInstance, sessionDuration);

            // Run post-launch command
            if (!string.IsNullOrWhiteSpace(SelectedInstance.PostLaunchCommand))
            {
                consoleViewModel?.AddLogEntry($"Running post-launch command: {SelectedInstance.PostLaunchCommand}", "INFO");
                await RunShellCommandAsync(SelectedInstance.PostLaunchCommand, SelectedInstance.GameDataPath);
            }

            StatusText = exitCode == 0 ? "Game closed successfully" : $"Game closed with exit code {exitCode}";
            ProgressValue = 0;
            ProgressText = "";

            consoleViewModel?.AddLogEntry(
                $"Game exited with code {exitCode}. Session duration: {sessionDuration:hh\\:mm\\:ss}",
                exitCode == 0 ? "INFO" : "WARN");

            _logger.Information("Game session completed. Exit code: {ExitCode}, Duration: {Duration}", exitCode, sessionDuration);

            if (exitCode != 0 && exitCode != -100)
            {
                var report = AnalyzeCrash(exitCode, SelectedInstance.Name, capturedLines);
                Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime dl)
                    {
                        var vm = new CrashReportViewModel(report);
                        var win = new Views.CrashReportWindow(vm);
                        await win.ShowDialog(dl.MainWindow!);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to launch instance");
            StatusText = "Launch failed";
            ProgressValue = 0;
            ProgressText = "";
            consoleViewModel?.AddLogEntry($"Launch failed: {ex.Message}", "ERROR");
        }
        finally
        {
            IsLaunching = false;
            
            // Unsubscribe from output events to prevent memory leaks
            if (outputHandler != null)
            {
                _gameLauncher.OutputReceived -= outputHandler;
            }
        }
    }

    private async Task CreateInstanceAsync()
    {
        try
        {
            _logger.Information("Create instance requested");
            
            var createViewModel = new CreateInstanceViewModel(_modLoaderService, _launcherSettings);
            var createWindow = new Views.CreateInstanceWindow(createViewModel);
            
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var result = await createWindow.ShowDialog<CreateInstanceViewModel?>(desktop.MainWindow!);
                
                if (result != null && result.Result)
                {
                    // Create the instance
                    StatusText = $"Creating instance '{result.InstanceName}'...";
                    _logger.Information("Creating instance: {InstanceName}, Version: {Version}", 
                        result.InstanceName, result.SelectedVersionId);
                    
                    var components = new System.Collections.Generic.List<Component>
                    {
                        new() { Uid = "net.minecraft", Version = result.SelectedVersionId, IsImportant = true }
                    };
                    
                    var newInstance = await _instanceManager.CreateInstanceAsync(
                        result.InstanceName,
                        components
                    );
                    
                    if (newInstance != null)
                    {
                        Instances.Add(newInstance);
                        SelectedInstance = newInstance;
                        StatusText = $"Instance '{result.InstanceName}' created successfully";
                        _logger.Information("Instance created successfully: {InstanceName}", result.InstanceName);
                    }
                    else
                    {
                        StatusText = "Failed to create instance";
                        _logger.Error("Failed to create instance: {InstanceName}", result.InstanceName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating instance");
            StatusText = "Error creating instance";
        }
    }

    private async Task EditInstanceAsync()
    {
        if (SelectedInstance == null)
            return;

        try
        {
            _logger.Information("Edit instance requested: {InstanceName}", SelectedInstance.Name);
            
            var settingsWindow = new Views.InstanceSettingsWindow(SelectedInstance);
            
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var result = await settingsWindow.ShowDialog<bool>(desktop.MainWindow!);
                
                if (result)
                {
                    // Reload the instance to reflect changes
                    await LoadInstancesAsync();
                    StatusText = $"Instance '{SelectedInstance.Name}' updated";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open instance settings");
            StatusText = "Failed to open instance settings";
        }
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

    private async void OpenAccountManagement()
    {
        try
        {
            _logger.Information("Opening account management");

            var accountWindow = new Views.AccountManagementWindow();

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                await accountWindow.ShowDialog(desktop.MainWindow!);
            }

            StatusText = "Account management closed";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open account management");
            StatusText = "Failed to open account management";
        }
    }

    private async void OpenLogViewer()
    {
        try
        {
            _logger.Information("Opening log viewer");

            // Default to launcher logs directory
            var logsPath = System.IO.Path.Combine(_launcherConfig.BaseDataPath, "logs");
            var logWindow = new Views.LogViewerWindow(logsPath);

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                await logWindow.ShowDialog(desktop.MainWindow!);
            }

            StatusText = "Log viewer closed";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open log viewer");
            StatusText = "Failed to open log viewer";
        }
    }

    private async void OpenScreenshotViewer()
    {
        try
        {
            _logger.Information("Opening screenshot viewer");

            // Show all screenshots from all instances
            var screenshotsPath = _launcherConfig.InstancesRootDir;
            var screenshotWindow = new Views.ScreenshotViewerWindow(screenshotsPath);

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                await screenshotWindow.ShowDialog(desktop.MainWindow!);
            }

            StatusText = "Screenshot viewer closed";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open screenshot viewer");
            StatusText = "Failed to open screenshot viewer";
        }
    }

    private void Exit()
    {
        System.Environment.Exit(0);
    }

    private static Models.CrashReport AnalyzeCrash(int exitCode, string instanceName, List<string> lines)
    {
        var relevant = new List<string>();
        var category = Models.CrashCategory.Unknown;
        string? suggestion = null;

        foreach (var line in lines)
        {
            if (line.Contains("OutOfMemoryError") || line.Contains("GC overhead limit exceeded"))
            {
                category = Models.CrashCategory.OutOfMemory;
                suggestion = "Increase Maximum Memory in Instance Settings → Java (try at least 4096 MB).";
                relevant.Add(line);
            }
            else if (line.Contains("hs_err_pid") || line.Contains("A fatal error has been detected by the Java Runtime Environment"))
            {
                category = Models.CrashCategory.JvmCrash;
                suggestion = "A JVM crash occurred. Check the hs_err_pid file in the instance folder for details.";
                relevant.Add(line);
            }
            else if (line.Contains("-- Mod List --") || line.Contains("Conflicting") || line.Contains("DuplicateMods"))
            {
                category = Models.CrashCategory.ModConflict;
                suggestion = "Remove recently added mods and try again. Check crash-reports/ for the full report.";
                relevant.Add(line);
            }
            else if (line.Contains("ClassNotFoundException") || line.Contains("NoClassDefFoundError") || line.Contains("ModLoadingException"))
            {
                if (category == Models.CrashCategory.Unknown)
                {
                    category = Models.CrashCategory.MissingDependency;
                    suggestion = "A required mod or library is missing. Check that all mod dependencies are installed.";
                }
                relevant.Add(line);
            }
            else if (line.Contains("---- Minecraft Crash Report ----") || line.Contains("A severe error has occurred"))
            {
                if (category == Models.CrashCategory.Unknown) category = Models.CrashCategory.GameCrash;
                relevant.Add(line);
            }
            else if (line.Contains("Exception") || line.Contains("FATAL") || line.Contains("ERROR"))
            {
                relevant.Add(line);
            }
        }

        if (relevant.Count > 40) relevant = relevant.GetRange(relevant.Count - 40, 40);

        return new Models.CrashReport
        {
            ExitCode = exitCode,
            InstanceName = instanceName,
            RelevantLines = relevant,
            Category = category,
            Suggestion = suggestion
        };
    }

    private async Task RunShellCommandAsync(string command, string workingDirectory)
    {
        try
        {
            string shell, shellArg;
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                shell = "cmd.exe";
                shellArg = $"/c {command}";
            }
            else
            {
                shell = "/bin/sh";
                shellArg = $"-c \"{command.Replace("\"", "\\\"")}\"";
            }

            using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = shell,
                Arguments = shellArg,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (proc != null) await proc.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Pre/post launch command failed: {Command}", command);
        }
    }

    private async Task ImportMultiMcAsync()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
            var files = await desktop.MainWindow!.StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Select MultiMC / Prism Launcher Export",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("Zip Archives") { Patterns = new[] { "*.zip" } }
                    }
                });

            if (files.Count == 0) return;
            var zipPath = files[0].Path.LocalPath;
            if (string.IsNullOrEmpty(zipPath)) return;

            StatusText = "Importing MultiMC instance...";
            var importer = new Services.Import.MultiMcImporter(_instanceManager);
            var instance = await importer.ImportAsync(zipPath);

            if (instance != null)
            {
                StatusText = $"Imported '{instance.Name}' successfully";
                await LoadInstancesAsync();
            }
            else
            {
                StatusText = "Import failed — check log for details";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MultiMC import failed");
            StatusText = "Import failed";
        }
    }

    private async Task CreateBackupAsync()
    {
        if (SelectedInstance == null) return;
        try
        {
            StatusText = $"Creating backup for '{SelectedInstance.Name}'...";
            var backup = await _backupManager.CreateBackupAsync(SelectedInstance);
            StatusText = backup != null ? $"Backup created: {backup.Name}" : "Backup failed";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Backup failed");
            StatusText = "Backup failed";
        }
    }

    private async void OpenJavaManager()
    {
        try
        {
            var vm = new JavaManagerViewModel(_javaManager);
            var win = new Views.JavaManagerWindow(vm);
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                await win.ShowDialog(desktop.MainWindow!);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open Java Manager");
        }
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
