using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using ObsidianLauncher.Services.Import;
using ObsidianLauncher.Services.Modrinth;
using ObsidianLauncher.Settings;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class ConfirmDeleteEventArgs : EventArgs
{
    public string InstanceName { get; }
    public System.Threading.Tasks.TaskCompletionSource<bool> Result { get; } = new();
    public ConfirmDeleteEventArgs(string name) { InstanceName = name; }
}

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
    private readonly ModrinthClient _modrinthClient;
    private readonly ModManager _modManager;
    private readonly UpdateChecker _updateChecker;
    private readonly AccountService _accountService;

    private Instance? _selectedInstance;
    private string _statusText;
    private bool _isLaunching;
    private double _progressValue;
    private string _progressText;
    private bool _isToolbarVisible = true;
    private bool _isStatusBarVisible = true;
    private string _latestNewsHeadline = "Loading news...";
    private string _searchFilter = "";
    private AccountInfo? _activeAccount;
    private string _mojangStatusText = "Checking...";

    // --- Routing ---
    private string _currentRoute = "instances";
    private bool _isDarkMode = false;
    private bool _isCreateInstanceOpen = false;
    private bool _isInstanceSettingsOpen = false;

    // --- Sub-ViewModels ---
    private InstanceSettingsViewModel? _currentInstanceSettingsVm;
    private CreateInstanceViewModel? _currentCreateVm;

    // --- Tab routing ---
    private string _instanceSettingsTab = "general";
    private string _settingsTab = "general";

    // --- Create instance wizard ---
    private int _createStep = 1;
    private string _newInstanceName = "";
    private string _newInstancePalette = "grass";

    // --- Console ---
    private bool _isConsolePaused = false;
    private string _javaVersionText = "Java";

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
        _modLoaderService = new ModLoaderService(_httpManager, _launcherConfig);
        _backupManager = new BackupManager(_launcherConfig);
        _modrinthClient = new ModrinthClient(_httpManager);
        _modManager = new ModManager(_modrinthClient);
        _updateChecker = new UpdateChecker(_httpManager);
        _accountService = new AccountService(_launcherConfig);

        Instances = new ObservableCollection<Instance>();
        Groups = new ObservableCollection<InstanceGroup>();
        Accounts = new ObservableCollection<AccountInfo>();
        Accounts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasAccounts));
        ScreenshotFiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasScreenshots));
        GameOutputLines = new ObservableCollection<string>();

        _statusText = "Ready";
        _progressText = "";
        _progressValue = 0;

        // Initialize commands
        LaunchInstanceCommand = new RelayCommand(async () => await LaunchInstanceAsync(), () => SelectedInstance != null && !IsLaunching);
        KillInstanceCommand = new RelayCommand(() => _gameLauncher.KillGame(), () => IsLaunching);
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
        ImportModrinthCommand = new RelayCommand(async () => await ImportModrinthAsync());
        ImportCurseForgeCommand = new RelayCommand(async () => await ImportCurseForgeAsync());
        ImportFtbCommand = new RelayCommand(async () => await ImportFtbAsync());
        ImportPackwizCommand = new RelayCommand(async () => await ImportPackwizAsync());
        CreateBackupCommand = new RelayCommand(async () => await CreateBackupAsync(), () => SelectedInstance != null);
        OpenBackupManagerCommand = new RelayCommand(OpenBackupManager);
        OpenJavaManagerCommand = new RelayCommand(OpenJavaManager);
        CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync());
        OpenModBrowserCommand = new RelayCommand(async () => await OpenModBrowserAsync(), () => SelectedInstance != null);
        OpenAboutCommand = new RelayCommand(async () => await OpenAboutAsync());
        OpenDocumentationCommand = new RelayCommand(OpenDocumentation);
        OpenReportBugCommand = new RelayCommand(OpenReportBug);
        ToggleToolbarCommand = new RelayCommand(() => IsToolbarVisible = !IsToolbarVisible);
        ToggleStatusBarCommand = new RelayCommand(() => IsStatusBarVisible = !IsStatusBarVisible);
        ViewInstanceFolderCommand = new RelayCommand(ViewInstanceFolder, () => SelectedInstance != null);
        ExportInstanceCommand = new RelayCommand(async () => await ExportInstanceAsync(), () => SelectedInstance != null);
        CopyInstanceCommand = new RelayCommand(async () => await CopyInstanceAsync(), () => SelectedInstance != null);
        CreateShortcutCommand = new RelayCommand(async () => await CreateShortcutAsync(), () => SelectedInstance != null);
        UndoTrashCommand = new RelayCommand(async () => await UndoTrashAsync());
        ChangeGroupCommand = new RelayCommand(async () => await ChangeGroupAsync(), () => SelectedInstance != null);
        OpenLauncherFolderCommand = new RelayCommand(() => OpenFolder(_launcherConfig.BaseDataPath));
        OpenInstancesFolderCommand = new RelayCommand(() => OpenFolder(_launcherConfig.InstancesRootDir));
        OpenLogsFolderCommand = new RelayCommand(() => OpenFolder(_launcherConfig.LogsDir));
        OpenDiscordCommand = new RelayCommand(() => OpenUrl("https://discord.gg/obsidian-launcher"));
        OpenRedditCommand = new RelayCommand(() => OpenUrl("https://reddit.com/r/feedthebeast"));
        OpenMoreNewsCommand = new RelayCommand(() => OpenUrl("https://www.minecraft.net/en-us/articles"));
        // Overlay close commands
        CloseCreateInstanceCommand = new RelayCommand(() =>
        {
            IsCreateInstanceOpen = false;
            CreateStep = 1;
            NewInstanceName = "";
            NewInstancePalette = "grass";
            CurrentCreateVm = null;
        });
        CloseInstanceSettingsCommand = new RelayCommand(() =>
        {
            IsInstanceSettingsOpen = false;
            CurrentInstanceSettingsVm = null;
            InstanceSettingsTab = "general";
        });
        SaveSelectedInstanceCommand = new RelayCommand(async () => await SaveSelectedInstanceAsync(), () => SelectedInstance != null);

        // New: tab navigation and wizard
        NavigateInstanceSettingsTabCommand = new ParamRelayCommand(obj => { if (obj is string t) InstanceSettingsTab = t; });
        NavigateSettingsTabCommand         = new ParamRelayCommand(obj => { if (obj is string t) SettingsTab = t; });
        AdvanceCreateStepCommand = new RelayCommand(async () => await AdvanceCreateStepAsync(), CanAdvanceCreateStep);
        RewindCreateStepCommand  = new RelayCommand(() => { if (CreateStep > 1) CreateStep--; });
        SetPaletteCommand        = new ParamRelayCommand(obj => { if (obj is string p) NewInstancePalette = p; });
        ToggleLightModeCommand   = new RelayCommand(() => SetTheme(false));
        ToggleDarkModeCommand    = new RelayCommand(() => SetTheme(true));
        OpenScreenshotsFolderCommand = new RelayCommand(OpenScreenshotsFolder);

        // Routing commands
        NavigateToInstancesCommand   = new RelayCommand(() => CurrentRoute = "instances");
        NavigateToModsCommand        = new RelayCommand(() => CurrentRoute = "mods");
        NavigateToScreenshotsCommand = new RelayCommand(() => CurrentRoute = "screenshots");
        NavigateToConsoleCommand     = new RelayCommand(() => CurrentRoute = "console");
        NavigateToAccountsCommand    = new RelayCommand(() => CurrentRoute = "accounts");
        NavigateToSettingsCommand    = new RelayCommand(() => CurrentRoute = "settings");

        AddOfflineAccountCommand  = new RelayCommand(async () => await AddOfflineAccountAsync());
        RemoveAccountCommand      = new RelayCommand(async () => await RemoveAccountAsync(), () => _activeAccount != null);
        SetActiveAccountCommand   = new ParamRelayCommand(obj => { if (obj is AccountInfo a) _ = SetAccountActiveByItemAsync(a); });

        // Load initial data
        _ = LoadInstancesAsync();
        _ = LoadGroupsAsync();
        _ = CheckForUpdatesAsync();
        _ = LoadNewsAsync();
        _ = LoadUserAccountsAsync();
        _ = CheckMojangStatusAsync();
    }

    public event EventHandler<ConfirmDeleteEventArgs>? ConfirmDeleteRequested;

    public ObservableCollection<Instance> Instances { get; }
    public ObservableCollection<InstanceGroup> Groups { get; }
    public ObservableCollection<AccountInfo> Accounts { get; }
    public ObservableCollection<ScreenshotItem> ScreenshotFiles { get; } = new();
    public ObservableCollection<string> GameOutputLines { get; }
    public bool HasScreenshots => ScreenshotFiles.Count > 0;

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
                ((RelayCommand)OpenModBrowserCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ViewInstanceFolderCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ExportInstanceCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CopyInstanceCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CreateShortcutCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ChangeGroupCommand).RaiseCanExecuteChanged();
                ((RelayCommand)SaveSelectedInstanceCommand).RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(SelectedInstanceJvmArgs));
            }
        }
    }

    public AccountInfo? ActiveAccount
    {
        get => _activeAccount;
        set
        {
            if (SetProperty(ref _activeAccount, value))
            {
                OnPropertyChanged(nameof(ActivePlayerName));
                OnPropertyChanged(nameof(ActivePlayerInitial));
                OnPropertyChanged(nameof(ActiveAccountTypeDisplay));
                ((RelayCommand)RemoveAccountCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string ActivePlayerName => _activeAccount?.Username ?? "Player";
    public string ActivePlayerInitial => string.IsNullOrEmpty(ActivePlayerName) ? "P" : ActivePlayerName[0].ToString().ToUpperInvariant();
    public string ActiveAccountTypeDisplay => _activeAccount?.TypeDisplay.ToLower() ?? "offline";
    public bool HasAccounts => Accounts.Count > 0;

    public string MojangStatusText
    {
        get => _mojangStatusText;
        set => SetProperty(ref _mojangStatusText, value);
    }

    public string SelectedInstanceJvmArgs
    {
        get => string.Join(" ", SelectedInstance?.CustomJvmArguments ?? new List<string>());
        set
        {
            if (SelectedInstance == null) return;
            SelectedInstance.CustomJvmArguments = string.IsNullOrWhiteSpace(value)
                ? new List<string>()
                : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            OnPropertyChanged();
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
                ((RelayCommand)KillInstanceCommand).RaiseCanExecuteChanged();
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

    public bool IsToolbarVisible
    {
        get => _isToolbarVisible;
        set => SetProperty(ref _isToolbarVisible, value);
    }

    public bool IsStatusBarVisible
    {
        get => _isStatusBarVisible;
        set => SetProperty(ref _isStatusBarVisible, value);
    }

    public string LatestNewsHeadline
    {
        get => _latestNewsHeadline;
        set => SetProperty(ref _latestNewsHeadline, value);
    }

    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            if (SetProperty(ref _searchFilter, value))
                ApplySearchFilter();
        }
    }

    private void ApplySearchFilter()
    {
        var filter = _searchFilter.Trim().ToLowerInvariant();
        foreach (var instance in Instances)
        {
            // Used by the ListBox filtering — requires a FilteredInstances collection
        }
        OnPropertyChanged(nameof(FilteredInstances));
    }

    public System.Collections.Generic.IEnumerable<Instance> FilteredInstances
    {
        get
        {
            var filter = _searchFilter.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(filter))
                return Instances;
            return Instances.Where(i =>
                i.Name.ToLowerInvariant().Contains(filter) ||
                (i.Notes?.ToLowerInvariant().Contains(filter) ?? false) ||
                (i.MinecraftVersionDisplay.ToLowerInvariant().Contains(filter)));
        }
    }

    public ICommand LaunchInstanceCommand { get; }
    public ICommand KillInstanceCommand { get; }
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
    public ICommand ImportModrinthCommand { get; }
    public ICommand ImportCurseForgeCommand { get; }
    public ICommand ImportFtbCommand { get; }
    public ICommand ImportPackwizCommand { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand OpenBackupManagerCommand { get; }
    public ICommand OpenJavaManagerCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }
    public ICommand OpenModBrowserCommand { get; }
    public ICommand OpenAboutCommand { get; }
    public ICommand OpenDocumentationCommand { get; }
    public ICommand OpenReportBugCommand { get; }
    public ICommand ToggleToolbarCommand { get; }
    public ICommand ToggleStatusBarCommand { get; }
    public ICommand ViewInstanceFolderCommand { get; }
    public ICommand ExportInstanceCommand { get; }
    public ICommand CopyInstanceCommand { get; }
    public ICommand CreateShortcutCommand { get; }
    public ICommand UndoTrashCommand { get; }
    public ICommand ChangeGroupCommand { get; }
    public ICommand OpenLauncherFolderCommand { get; }
    public ICommand OpenInstancesFolderCommand { get; }
    public ICommand OpenLogsFolderCommand { get; }
    public ICommand OpenDiscordCommand { get; }
    public ICommand OpenRedditCommand { get; }
    public ICommand OpenMoreNewsCommand { get; }
    // --- Overlay close commands ---
    public ICommand CloseCreateInstanceCommand { get; }
    public ICommand CloseInstanceSettingsCommand { get; }
    public ICommand SaveSelectedInstanceCommand { get; }

    // --- New: tab navigation, wizard, theme, screenshots ---
    public ICommand NavigateInstanceSettingsTabCommand { get; }
    public ICommand NavigateSettingsTabCommand { get; }
    public ICommand AdvanceCreateStepCommand { get; }
    public ICommand RewindCreateStepCommand { get; }
    public ICommand SetPaletteCommand { get; }
    public ICommand ToggleLightModeCommand { get; }
    public ICommand ToggleDarkModeCommand { get; }
    public ICommand OpenScreenshotsFolderCommand { get; }

    // --- Account commands ---
    public ICommand AddOfflineAccountCommand { get; }
    public ICommand RemoveAccountCommand { get; }
    public ICommand SetActiveAccountCommand { get; }

    // --- Routing commands ---
    public ICommand NavigateToInstancesCommand { get; }
    public ICommand NavigateToModsCommand { get; }
    public ICommand NavigateToScreenshotsCommand { get; }
    public ICommand NavigateToConsoleCommand { get; }
    public ICommand NavigateToAccountsCommand { get; }
    public ICommand NavigateToSettingsCommand { get; }

    public string CurrentRoute
    {
        get => _currentRoute;
        set
        {
            if (SetProperty(ref _currentRoute, value))
            {
                OnPropertyChanged(nameof(IsInstancesScreen));
                OnPropertyChanged(nameof(IsModsScreen));
                OnPropertyChanged(nameof(IsScreenshotsScreen));
                OnPropertyChanged(nameof(IsConsoleScreen));
                OnPropertyChanged(nameof(IsAccountsScreen));
                OnPropertyChanged(nameof(IsSettingsScreen));
                if (value == "screenshots") _ = LoadScreenshotsAsync();
            }
        }
    }

    public bool IsInstancesScreen  => CurrentRoute == "instances";
    public bool IsModsScreen       => CurrentRoute == "mods";
    public bool IsScreenshotsScreen => CurrentRoute == "screenshots";
    public bool IsConsoleScreen    => CurrentRoute == "console";
    public bool IsAccountsScreen   => CurrentRoute == "accounts";
    public bool IsSettingsScreen   => CurrentRoute == "settings";

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set => SetProperty(ref _isDarkMode, value);
    }

    public bool IsCreateInstanceOpen
    {
        get => _isCreateInstanceOpen;
        set => SetProperty(ref _isCreateInstanceOpen, value);
    }

    public bool IsInstanceSettingsOpen
    {
        get => _isInstanceSettingsOpen;
        set => SetProperty(ref _isInstanceSettingsOpen, value);
    }

    // --- Sub-ViewModels ---
    public InstanceSettingsViewModel? CurrentInstanceSettingsVm
    {
        get => _currentInstanceSettingsVm;
        set => SetProperty(ref _currentInstanceSettingsVm, value);
    }

    public CreateInstanceViewModel? CurrentCreateVm
    {
        get => _currentCreateVm;
        set => SetProperty(ref _currentCreateVm, value);
    }

    // --- Instance settings tab routing ---
    public string InstanceSettingsTab
    {
        get => _instanceSettingsTab;
        set
        {
            if (SetProperty(ref _instanceSettingsTab, value))
            {
                OnPropertyChanged(nameof(IsInstanceSettingsGeneralTab));
                OnPropertyChanged(nameof(IsInstanceSettingsJavaTab));
                OnPropertyChanged(nameof(IsInstanceSettingsGameTab));
                OnPropertyChanged(nameof(IsInstanceSettingsModLoaderTab));
                OnPropertyChanged(nameof(IsInstanceSettingsCommandsTab));
            }
        }
    }

    public bool IsInstanceSettingsGeneralTab   => _instanceSettingsTab == "general";
    public bool IsInstanceSettingsJavaTab      => _instanceSettingsTab == "java";
    public bool IsInstanceSettingsGameTab      => _instanceSettingsTab == "game";
    public bool IsInstanceSettingsModLoaderTab => _instanceSettingsTab == "modloader";
    public bool IsInstanceSettingsCommandsTab  => _instanceSettingsTab == "commands";

    // --- Settings screen tab routing ---
    public string SettingsTab
    {
        get => _settingsTab;
        set
        {
            if (SetProperty(ref _settingsTab, value))
            {
                OnPropertyChanged(nameof(IsSettingsGeneralTab));
                OnPropertyChanged(nameof(IsSettingsAppearanceTab));
                OnPropertyChanged(nameof(IsSettingsDownloadsTab));
                OnPropertyChanged(nameof(IsSettingsJavaTab));
                OnPropertyChanged(nameof(IsSettingsAboutTab));
            }
        }
    }

    public bool IsSettingsGeneralTab    => _settingsTab == "general";
    public bool IsSettingsAppearanceTab => _settingsTab == "appearance";
    public bool IsSettingsDownloadsTab  => _settingsTab == "downloads";
    public bool IsSettingsJavaTab       => _settingsTab == "java";
    public bool IsSettingsAboutTab      => _settingsTab == "about";

    // --- Create instance wizard ---
    public int CreateStep
    {
        get => _createStep;
        set
        {
            if (SetProperty(ref _createStep, value))
            {
                OnPropertyChanged(nameof(IsCreateStep1));
                OnPropertyChanged(nameof(IsCreateStep2));
                OnPropertyChanged(nameof(IsCreateStep3));
                OnPropertyChanged(nameof(CreateStepLabel));
                OnPropertyChanged(nameof(CreateStepTitle));
                ((RelayCommand)AdvanceCreateStepCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsCreateStep1     => _createStep == 1;
    public bool IsCreateStep2     => _createStep == 2;
    public bool IsCreateStep3     => _createStep == 3;
    public string CreateStepLabel => $"Step {_createStep} of 3";
    public string CreateStepTitle => _createStep switch
    {
        1 => "A new instance, then?",
        2 => "Pick a version",
        3 => "Looks good!",
        _ => ""
    };

    public string NewInstanceName
    {
        get => _newInstanceName;
        set => SetProperty(ref _newInstanceName, value);
    }

    public string NewInstancePalette
    {
        get => _newInstancePalette;
        set => SetProperty(ref _newInstancePalette, value);
    }

    // --- Console ---
    public bool IsConsolePaused
    {
        get => _isConsolePaused;
        set
        {
            if (SetProperty(ref _isConsolePaused, value))
                OnPropertyChanged(nameof(ConsolePauseButtonLabel));
        }
    }

    public string ConsolePauseButtonLabel => _isConsolePaused ? "Resume" : "Pause";

    public string JavaVersionText
    {
        get => _javaVersionText;
        set => SetProperty(ref _javaVersionText, value);
    }

    // --- Settings passthrough properties ---
    public bool ShowConsoleOnLaunch
    {
        get => _launcherSettings.ShowConsoleOnLaunch.Value;
        set { _launcherSettings.ShowConsoleOnLaunch.Value = value; _launcherSettings.Save(); OnPropertyChanged(); }
    }

    public bool CloseAfterLaunch
    {
        get => _launcherSettings.CloseAfterLaunch.Value;
        set { _launcherSettings.CloseAfterLaunch.Value = value; _launcherSettings.Save(); OnPropertyChanged(); }
    }

    public bool CheckForUpdatesOnStartup
    {
        get => _launcherSettings.CheckForUpdates.Value;
        set { _launcherSettings.CheckForUpdates.Value = value; _launcherSettings.Save(); OnPropertyChanged(); }
    }

    public string GlobalJavaPath
    {
        get => _launcherSettings.JavaPath.Value;
        set { _launcherSettings.JavaPath.Value = value; _launcherSettings.Save(); OnPropertyChanged(); }
    }

    public int GlobalMinMemoryMB
    {
        get => _launcherSettings.MinMemoryMB.Value;
        set { _launcherSettings.MinMemoryMB.Value = value; _launcherSettings.Save(); OnPropertyChanged(); }
    }

    public int GlobalMaxMemoryMB
    {
        get => _launcherSettings.MaxMemoryMB.Value;
        set { _launcherSettings.MaxMemoryMB.Value = value; _launcherSettings.Save(); OnPropertyChanged(); }
    }

    public string GlobalJavaArgs
    {
        get => _launcherSettings.JavaArgs.Value;
        set { _launcherSettings.JavaArgs.Value = value; _launcherSettings.Save(); OnPropertyChanged(); }
    }

    public int MaxConcurrentDownloads
    {
        get => _launcherSettings.MaxConcurrentDownloads.Value;
        set { _launcherSettings.MaxConcurrentDownloads.Value = value; _launcherSettings.Save(); OnPropertyChanged(); }
    }

    public bool GlobalFullscreen
    {
        get => _launcherSettings.Fullscreen.Value;
        set { _launcherSettings.Fullscreen.Value = value; _launcherSettings.Save(); OnPropertyChanged(); }
    }

    private async Task LoadInstancesAsync()
    {
        try
        {
            StatusText = "Loading instances...";
            var instances = await _instanceManager.GetAllInstancesAsync();

            // Build a lookup of group ID → group name for display
            var groupById = Groups.ToDictionary(g => g.Id, g => g);

            Instances.Clear();
            foreach (var instance in instances.OrderBy(i => i.SortOrder).ThenBy(i => i.Name))
            {
                // Populate the display-only GroupDisplayName property
                instance.GroupDisplayName = instance.GroupId != null && groupById.TryGetValue(instance.GroupId, out var grp)
                    ? grp.Name
                    : null;
                Instances.Add(instance);
            }

            StatusText = $"Loaded {Instances.Count} instance(s)";
            OnPropertyChanged(nameof(FilteredInstances));
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
            CurrentRoute = "console";
            StatusText = $"Launching {SelectedInstance.Name}...";
            ProgressValue = 0;
            ProgressText = "Preparing...";

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                GameOutputLines.Clear();
                GameOutputLines.Add($"Launching {SelectedInstance.Name}...");
            });

            _logger.Information("Launching instance: {InstanceName}", SelectedInstance.Name);

            // Create and show console window
            consoleViewModel = new ConsoleViewModel();
            consoleWindow = new Views.ConsoleWindow(consoleViewModel);
            consoleWindow.Show();
            consoleViewModel.AddLogEntry($"Launching {SelectedInstance.Name}...", "INFO");

            // Resolve launch artifacts (profile + file paths) without downloading.
            // All downloads happen at instance creation time via SyncInstanceAsync.
            consoleViewModel?.AddLogEntry("Resolving launch artifacts...", "INFO");
            var (launchProfile, clientJarPath, libraryJarPaths) =
                await _instanceManager.ResolveLaunchArtifactsAsync(SelectedInstance);

            if (launchProfile == null)
            {
                StatusText = "Failed to build launch profile";
                _logger.Error("Failed to build launch profile for instance: {InstanceName}", SelectedInstance.Name);
                consoleViewModel?.AddLogEntry("Failed to build launch profile", "ERROR");
                return;
            }

            if (string.IsNullOrEmpty(clientJarPath) || !File.Exists(clientJarPath))
            {
                StatusText = "Instance files missing — please repair or re-create the instance";
                _logger.Error("Client JAR not found at '{ClientJarPath}' for '{InstanceName}'.", clientJarPath, SelectedInstance.Name);
                consoleViewModel?.AddLogEntry("Instance files missing. Repair or re-create the instance.", "ERROR");
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
            JavaVersionText = $"Java {javaRuntime.MajorVersion}";

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

                    if (!IsConsolePaused)
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            GameOutputLines.Add(line);
                            if (GameOutputLines.Count > 2000) GameOutputLines.RemoveAt(0);
                        });
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
            CreateStep = 1;
            NewInstanceName = "";
            NewInstancePalette = "grass";
            CurrentCreateVm = null;
            IsCreateInstanceOpen = true;
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
            InstanceSettingsTab = "general";
            CurrentInstanceSettingsVm = new InstanceSettingsViewModel(SelectedInstance, _launcherSettings);
            IsInstanceSettingsOpen = true;
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

        var args = new ConfirmDeleteEventArgs(SelectedInstance.Name);
        ConfirmDeleteRequested?.Invoke(this, args);
        var confirmed = await args.Result.Task;
        if (!confirmed) return;

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

    private void OpenSettings()
    {
        CurrentRoute = "settings";
        StatusText = "Settings";
    }

    private void OpenAccountManagement()
    {
        CurrentRoute = "accounts";
        StatusText = "Accounts";
    }

    private async void OpenLogViewer()
    {
        try
        {
            _logger.Information("Opening log viewer");

            var logsPath = System.IO.Path.Combine(_launcherConfig.BaseDataPath, "logs");
            var logWindow = new Views.LogViewerWindow(logsPath, InMemoryLogSink.Instance);

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

    private async Task OpenAboutAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

        var closeBtn = new Avalonia.Controls.Button
        {
            Content = "Close",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Padding = new Avalonia.Thickness(20, 6)
        };

        var dlg = new Avalonia.Controls.Window
        {
            Title = "About Obsidian Launcher",
            Width = 380,
            Height = 200,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new Avalonia.Controls.StackPanel
            {
                Margin = new Avalonia.Thickness(24),
                Spacing = 8,
                Children =
                {
                    new Avalonia.Controls.TextBlock
                    {
                        Text = "Obsidian Launcher",
                        FontSize = 20,
                        FontWeight = Avalonia.Media.FontWeight.Bold
                    },
                    new Avalonia.Controls.TextBlock
                    {
                        Text = $"Version {LauncherConfig.VERSION}",
                        Opacity = 0.7
                    },
                    new Avalonia.Controls.TextBlock
                    {
                        Text = "A Minecraft launcher built with Avalonia and .NET 10.",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    closeBtn
                }
            }
        };

        closeBtn.Click += (_, _) => dlg.Close();
        await dlg.ShowDialog(desktop.MainWindow!);
    }

    private void OpenDocumentation()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/Advik-B/obsidian-launcher#readme",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open documentation URL");
        }
    }

    private void OpenReportBug()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/Advik-B/Obsidian-Launcher/issues/new",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open bug report URL");
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open folder: {Path}", path);
            StatusText = $"Could not open folder: {path}";
        }
    }

    private void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open URL: {Url}", url);
        }
    }

    private void ViewInstanceFolder()
    {
        if (SelectedInstance == null) return;
        OpenFolder(SelectedInstance.InstancePath);
    }

    private async Task ExportInstanceAsync()
    {
        if (SelectedInstance == null) return;
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

            var file = await desktop.MainWindow!.StorageProvider.SaveFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Export Instance",
                    SuggestedFileName = $"{SelectedInstance.Name}.zip",
                    DefaultExtension = "zip",
                    FileTypeChoices = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("Zip Archive") { Patterns = new[] { "*.zip" } }
                    }
                });

            if (file == null) return;
            var outputPath = file.Path.LocalPath;
            if (string.IsNullOrEmpty(outputPath)) return;

            StatusText = $"Exporting '{SelectedInstance.Name}'...";
            var result = await _instanceManager.ExportInstanceToZipAsync(SelectedInstance, outputPath);
            StatusText = result != null
                ? $"Exported '{SelectedInstance.Name}' to {System.IO.Path.GetFileName(outputPath)}"
                : "Export failed — check log for details";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Export instance failed");
            StatusText = "Export failed";
        }
    }

    private async Task CopyInstanceAsync()
    {
        if (SelectedInstance == null) return;
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

            var nameBox = new Avalonia.Controls.TextBox
            {
                PlaceholderText = "New instance name",
                Width = 260,
                Text = $"{SelectedInstance.Name} (Copy)"
            };
            var okBtn = new Avalonia.Controls.Button
            {
                Content = "Copy",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };
            var cancelBtn = new Avalonia.Controls.Button
            {
                Content = "Cancel",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };

            var dlg = new Avalonia.Controls.Window
            {
                Title = "Copy Instance",
                Width = 360,
                Height = 180,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                CanResize = false,
                Content = new Avalonia.Controls.StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Spacing = 12,
                    Children =
                    {
                        new Avalonia.Controls.TextBlock
                        {
                            Text = $"Enter a name for the copy of '{SelectedInstance.Name}':",
                            FontWeight = Avalonia.Media.FontWeight.SemiBold,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        },
                        nameBox,
                        new Avalonia.Controls.StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Spacing = 8,
                            Children = { cancelBtn, okBtn }
                        }
                    }
                }
            };

            string? newName = null;
            okBtn.Click += (_, _) => { newName = nameBox.Text; dlg.Close(true); };
            cancelBtn.Click += (_, _) => dlg.Close(false);

            var ok = await dlg.ShowDialog<bool>(desktop.MainWindow!);
            if (!ok || string.IsNullOrWhiteSpace(newName)) return;

            StatusText = $"Copying '{SelectedInstance.Name}'...";
            var copy = await _instanceManager.CopyInstanceAsync(SelectedInstance, newName.Trim());
            if (copy != null)
            {
                await LoadInstancesAsync();
                SelectedInstance = copy;
                StatusText = $"Copied as '{copy.Name}'";
            }
            else
            {
                StatusText = "Copy failed — check log for details";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Copy instance failed");
            StatusText = "Copy failed";
        }
    }

    private async Task CreateShortcutAsync()
    {
        if (SelectedInstance == null) return;
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
                StatusText = "Could not determine launcher executable path";
                return;
            }

            var instanceName = SelectedInstance.Name;
            var desktopDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                // Create .lnk via PowerShell
                var lnkPath = System.IO.Path.Combine(desktopDir, $"{instanceName}.lnk");
                var psScript = $"$WshShell = New-Object -comObject WScript.Shell; " +
                               $"$Shortcut = $WshShell.CreateShortcut('{lnkPath.Replace("'", "''")}'); " +
                               $"$Shortcut.TargetPath = '{exePath.Replace("'", "''")}'; " +
                               $"$Shortcut.Arguments = '--instance \"{instanceName}\"'; " +
                               $"$Shortcut.Description = 'Launch Obsidian Launcher - {instanceName}'; " +
                               $"$Shortcut.Save()";

                using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"{psScript.Replace("\"", "\\\"")}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (proc != null) await proc.WaitForExitAsync();
                StatusText = $"Shortcut created on desktop: {instanceName}.lnk";
            }
            else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                var desktopFilePath = System.IO.Path.Combine(desktopDir, $"{instanceName}.desktop");
                var desktopFileContent =
                    $"[Desktop Entry]\n" +
                    $"Type=Application\n" +
                    $"Name=Obsidian Launcher - {instanceName}\n" +
                    $"Exec=\"{exePath}\" --instance \"{instanceName}\"\n" +
                    $"Terminal=false\n" +
                    $"Comment=Launch Minecraft instance: {instanceName}\n";
                await System.IO.File.WriteAllTextAsync(desktopFilePath, desktopFileContent);
                // Make executable
                using var chmod = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{desktopFilePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (chmod != null) await chmod.WaitForExitAsync();
                StatusText = $"Shortcut created on desktop: {instanceName}.desktop";
            }
            else
            {
                StatusText = "Desktop shortcut creation is not supported on this platform";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Create shortcut failed");
            StatusText = "Shortcut creation failed";
        }
    }

    private async Task UndoTrashAsync()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

            // Enumerate auto-trash-backup directories and extract unique instance names
            var backupsDir = System.IO.Path.Combine(_launcherConfig.BaseDataPath, "backups", "instances");
            var deletedNames = new System.Collections.Generic.List<string>();
            if (System.IO.Directory.Exists(backupsDir))
            {
                var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var dir in System.IO.Directory.GetDirectories(backupsDir))
                {
                    var dirName = System.IO.Path.GetFileName(dir);
                    // Strip trailing _yyyyMMdd_HHmmss suffix (17 chars: _YYYYMMDD_HHMMSS)
                    var lastUnderscore = dirName.LastIndexOf('_');
                    if (lastUnderscore > 0)
                    {
                        var secondLast = dirName.LastIndexOf('_', lastUnderscore - 1);
                        if (secondLast > 0)
                        {
                            var name = dirName.Substring(0, secondLast);
                            if (seen.Add(name))
                                deletedNames.Add(name);
                        }
                    }
                }
            }

            if (deletedNames.Count == 0)
            {
                var infoDlg = new Avalonia.Controls.Window
                {
                    Title = "Undo Trash",
                    Width = 340,
                    Height = 130,
                    WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                    CanResize = false,
                    Content = new Avalonia.Controls.StackPanel
                    {
                        Margin = new Avalonia.Thickness(20),
                        Spacing = 12,
                        Children =
                        {
                            new Avalonia.Controls.TextBlock
                            {
                                Text = "No deleted instances available to restore.",
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap
                            },
                            new Avalonia.Controls.Button
                            {
                                Content = "OK",
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
                            }
                        }
                    }
                };
                var okInner = (Avalonia.Controls.Button)((Avalonia.Controls.StackPanel)infoDlg.Content!).Children[1];
                okInner.Click += (_, _) => infoDlg.Close();
                await infoDlg.ShowDialog(desktop.MainWindow!);
                return;
            }

            var listBox = new Avalonia.Controls.ListBox
            {
                ItemsSource = deletedNames,
                SelectedIndex = 0,
                Width = 300,
                Height = Math.Min(deletedNames.Count * 32 + 8, 200)
            };
            var restoreBtn = new Avalonia.Controls.Button { Content = "Restore", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
            var cancelBtn = new Avalonia.Controls.Button { Content = "Cancel", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };

            var dlg = new Avalonia.Controls.Window
            {
                Title = "Undo Trash Instance",
                Width = 360,
                SizeToContent = Avalonia.Controls.SizeToContent.Height,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                CanResize = false,
                Content = new Avalonia.Controls.StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Spacing = 12,
                    Children =
                    {
                        new Avalonia.Controls.TextBlock
                        {
                            Text = "Select a deleted instance to restore:",
                            FontWeight = Avalonia.Media.FontWeight.SemiBold
                        },
                        listBox,
                        new Avalonia.Controls.StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Spacing = 8,
                            Children = { cancelBtn, restoreBtn }
                        }
                    }
                }
            };

            string? instanceName = null;
            restoreBtn.Click += (_, _) => { instanceName = listBox.SelectedItem as string; dlg.Close(true); };
            cancelBtn.Click += (_, _) => dlg.Close(false);
            listBox.DoubleTapped += (_, _) => { instanceName = listBox.SelectedItem as string; dlg.Close(true); };

            var ok = await dlg.ShowDialog<bool>(desktop.MainWindow!);
            if (!ok || string.IsNullOrWhiteSpace(instanceName)) return;

            StatusText = $"Restoring '{instanceName}'...";
            var restored = await _instanceManager.UndoTrashAsync(instanceName);
            if (restored != null)
            {
                await LoadInstancesAsync();
                SelectedInstance = restored;
                StatusText = $"Restored instance '{restored.Name}'";
            }
            else
            {
                StatusText = $"Could not restore '{instanceName}' — no backup found or restore failed";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Undo trash failed");
            StatusText = "Restore failed";
        }
    }

    private async Task ChangeGroupAsync()
    {
        if (SelectedInstance == null) return;
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

            const string Ungrouped = "(Ungrouped)";
            const string NewGroup = "— New group —";

            // Build items: (Ungrouped) + existing groups + New group sentinel
            var groupItems = new System.Collections.Generic.List<string> { Ungrouped };
            groupItems.AddRange(Groups.Select(g => g.Name));
            groupItems.Add(NewGroup);

            var currentGroupName = SelectedInstance.GroupDisplayName;
            var initialSelection = string.IsNullOrEmpty(currentGroupName) ? Ungrouped : currentGroupName;

            var groupCombo = new Avalonia.Controls.ComboBox
            {
                ItemsSource = groupItems,
                SelectedItem = groupItems.Contains(initialSelection) ? initialSelection : Ungrouped,
                Width = 280
            };
            var newGroupBox = new Avalonia.Controls.TextBox
            {
                PlaceholderText = "New group name",
                Width = 280,
                IsVisible = false
            };

            groupCombo.SelectionChanged += (_, _) =>
            {
                newGroupBox.IsVisible = groupCombo.SelectedItem as string == NewGroup;
            };

            var okBtn = new Avalonia.Controls.Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
            var cancelBtn = new Avalonia.Controls.Button { Content = "Cancel", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };

            var dlg = new Avalonia.Controls.Window
            {
                Title = "Change Group",
                Width = 380,
                SizeToContent = Avalonia.Controls.SizeToContent.Height,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                CanResize = false,
                Content = new Avalonia.Controls.StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Spacing = 12,
                    Children =
                    {
                        new Avalonia.Controls.TextBlock
                        {
                            Text = $"Select group for '{SelectedInstance.Name}':",
                            FontWeight = Avalonia.Media.FontWeight.SemiBold,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        },
                        groupCombo,
                        newGroupBox,
                        new Avalonia.Controls.StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Spacing = 8,
                            Children = { cancelBtn, okBtn }
                        }
                    }
                }
            };

            string? resolvedSelection = null;
            okBtn.Click += (_, _) =>
            {
                resolvedSelection = groupCombo.SelectedItem as string == NewGroup
                    ? newGroupBox.Text
                    : groupCombo.SelectedItem as string;
                dlg.Close(true);
            };
            cancelBtn.Click += (_, _) => dlg.Close(false);

            var ok = await dlg.ShowDialog<bool>(desktop.MainWindow!);
            if (!ok) return;

            var instance = SelectedInstance;

            if (string.IsNullOrWhiteSpace(resolvedSelection) || resolvedSelection == Ungrouped)
            {
                instance.GroupId = null;
            }
            else
            {
                var groupName = resolvedSelection.Trim();
                var existing = Groups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    instance.GroupId = existing.Id;
                }
                else
                {
                    var newGroup = _groupManager.CreateGroup(groupName);
                    Groups.Add(newGroup);
                    instance.GroupId = newGroup.Id;
                }
            }

            await _instanceManager.SaveInstanceAsync(instance);
            await LoadInstancesAsync();
            StatusText = $"Group updated for '{instance.Name}'";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Change group failed");
            StatusText = "Failed to change group";
        }
    }

    private async Task SaveSelectedInstanceAsync()
    {
        if (SelectedInstance == null) return;
        try
        {
            CurrentInstanceSettingsVm?.SaveCommand.Execute(null);
            await _instanceManager.SaveInstanceAsync(SelectedInstance);
            StatusText = $"Saved '{SelectedInstance.Name}'";
            _logger.Information("Instance settings saved: {Name}", SelectedInstance.Name);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save instance settings");
            StatusText = "Failed to save settings";
        }
        finally
        {
            IsInstanceSettingsOpen = false;
            CurrentInstanceSettingsVm = null;
            InstanceSettingsTab = "general";
        }
    }

    public CreateInstanceViewModel CreateNewInstanceVM()
    {
        var vm = new CreateInstanceViewModel(_httpManager, _instanceManager, _launcherSettings);
        vm.CreationCompleted += (_, _) =>
        {
            _ = LoadInstancesAsync();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsCreateInstanceOpen = false;
                CreateStep = 1;
                NewInstanceName = "";
                NewInstancePalette = "grass";
                CurrentCreateVm = null;
            });
        };
        return vm;
    }

    private async Task LoadUserAccountsAsync()
    {
        try
        {
            var accounts = await _accountService.LoadAccountsAsync();
            Accounts.Clear();
            foreach (var a in accounts) Accounts.Add(a);
            ActiveAccount = accounts.FirstOrDefault(a => a.IsActive) ?? accounts.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load accounts");
        }
    }

    private async Task AddOfflineAccountAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

        var nameBox = new Avalonia.Controls.TextBox
        {
            PlaceholderText = "Player name",
            Width = 240
        };
        var okBtn    = new Avalonia.Controls.Button { Content = "Add",    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Padding = new Avalonia.Thickness(16, 8) };
        var cancelBtn = new Avalonia.Controls.Button { Content = "Cancel", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Padding = new Avalonia.Thickness(16, 8) };

        var dlg = new Avalonia.Controls.Window
        {
            Title = "Add Offline Account",
            Width = 340,
            Height = 170,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new Avalonia.Controls.StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 14,
                Children =
                {
                    new Avalonia.Controls.TextBlock { Text = "Enter a player name:", FontWeight = Avalonia.Media.FontWeight.SemiBold },
                    nameBox,
                    new Avalonia.Controls.StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancelBtn, okBtn }
                    }
                }
            }
        };

        string? name = null;
        okBtn.Click    += (_, _) => { name = nameBox.Text; dlg.Close(); };
        cancelBtn.Click += (_, _) => dlg.Close();

        await dlg.ShowDialog(desktop.MainWindow!);
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            var account = await _accountService.AddOfflineAccountAsync(name.Trim());
            Accounts.Add(account);
            if (Accounts.Count == 1)
            {
                await _accountService.SetActiveAccountAsync(account.Id);
                account.IsActive = true;
                ActiveAccount = account;
            }
            StatusText = $"Added offline account: {account.Username}";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to add offline account");
            StatusText = "Failed to add account";
        }
    }

    private async Task RemoveAccountAsync()
    {
        if (_activeAccount == null) return;
        try
        {
            await _accountService.RemoveAccountAsync(_activeAccount.Id);
            Accounts.Remove(_activeAccount);
            ActiveAccount = Accounts.FirstOrDefault(a => a.IsActive) ?? Accounts.FirstOrDefault();
            StatusText = "Account removed";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to remove account");
        }
    }

    private async Task SetAccountActiveByItemAsync(AccountInfo account)
    {
        try
        {
            await _accountService.SetActiveAccountAsync(account.Id);
            foreach (var a in Accounts) a.IsActive = a.Id == account.Id;
            ActiveAccount = account;
            StatusText = $"Active account: {account.Username}";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set active account");
        }
    }

    private async Task CheckMojangStatusAsync()
    {
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.Add("User-Agent", "ObsidianLauncher/1.0");
            var resp = await client.GetAsync("https://sessionserver.mojang.com/");
            MojangStatusText = resp.IsSuccessStatusCode ? "Mojang OK" : "Mojang degraded";
        }
        catch
        {
            MojangStatusText = "Mojang offline";
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

    private async Task ImportModrinthAsync()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
            var files = await desktop.MainWindow!.StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Select Modrinth Modpack (.mrpack)",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("Modrinth Modpack") { Patterns = new[] { "*.mrpack" } }
                    }
                });

            if (files.Count == 0) return;
            var mrpackPath = files[0].Path.LocalPath;
            if (string.IsNullOrEmpty(mrpackPath)) return;

            StatusText = "Importing Modrinth modpack...";
            var importer = new ModrinthImporter(_instanceManager, _httpManager);
            var progress = new Progress<(string Status, double Progress)>(r =>
            {
                StatusText = r.Status;
                ProgressValue = r.Progress;
            });

            var instance = await importer.ImportAsync(mrpackPath, progress);
            ProgressValue = 0;
            if (instance != null)
            {
                StatusText = $"Imported '{instance.Name}' successfully";
                await LoadInstancesAsync();
            }
            else
            {
                StatusText = "Modrinth import failed — check log for details";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Modrinth import failed");
            StatusText = "Import failed";
        }
    }

    private async Task ImportCurseForgeAsync()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
            var files = await desktop.MainWindow!.StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Select CurseForge Modpack (.zip)",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("CurseForge Modpack") { Patterns = new[] { "*.zip" } }
                    }
                });

            if (files.Count == 0) return;
            var zipPath = files[0].Path.LocalPath;
            if (string.IsNullOrEmpty(zipPath)) return;

            StatusText = "Importing CurseForge modpack...";
            var importer = new CurseForgeImporter(_instanceManager, _httpManager);
            var progress = new Progress<(string Status, double Progress)>(r =>
            {
                StatusText = r.Status;
                ProgressValue = r.Progress;
            });

            var instance = await importer.ImportAsync(zipPath, progress);
            ProgressValue = 0;
            if (instance != null)
            {
                StatusText = $"Imported '{instance.Name}' successfully";
                await LoadInstancesAsync();
            }
            else
            {
                StatusText = "CurseForge import failed — check log for details";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "CurseForge import failed");
            StatusText = "Import failed";
        }
    }

    private async Task ImportFtbAsync()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
            var win = new Views.FtbBrowserWindow(_instanceManager, _httpManager);
            await win.ShowDialog(desktop.MainWindow!);
            await LoadInstancesAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "FTB import failed");
            StatusText = "Import failed";
        }
    }

    private async Task ImportPackwizAsync()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

            var urlBox = new Avalonia.Controls.TextBox
            {
                PlaceholderText = "https://example.com/pack.toml",
                Width = 360,
                MinWidth = 300
            };
            var importBtn = new Avalonia.Controls.Button { Content = "Import", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
            var cancelBtn = new Avalonia.Controls.Button { Content = "Cancel", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };

            var dlg = new Avalonia.Controls.Window
            {
                Title = "Import Packwiz Pack",
                Width = 440,
                Height = 160,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                CanResize = false,
                Content = new Avalonia.Controls.StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Spacing = 12,
                    Children =
                    {
                        new Avalonia.Controls.TextBlock { Text = "Enter the pack.toml URL:", FontWeight = Avalonia.Media.FontWeight.SemiBold },
                        urlBox,
                        new Avalonia.Controls.StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Spacing = 8,
                            Children = { cancelBtn, importBtn }
                        }
                    }
                }
            };

            string? url = null;
            importBtn.Click += (_, _) => { url = urlBox.Text; dlg.Close(true); };
            cancelBtn.Click += (_, _) => dlg.Close(false);

            var ok = await dlg.ShowDialog<bool>(desktop.MainWindow!);
            if (!ok || string.IsNullOrWhiteSpace(url)) return;

            StatusText = "Importing Packwiz pack...";
            var progress = new Progress<(string Status, double Progress)>(r =>
            {
                StatusText = r.Status;
                ProgressValue = r.Progress * 100;
            });

            using var cts = new System.Threading.CancellationTokenSource();
            var importer = new Services.Import.PackwizImporter(_instanceManager, _httpManager);
            var instance = await importer.ImportFromUrlAsync(url.Trim(), progress, cts.Token);
            ProgressValue = 0;
            if (instance != null)
            {
                StatusText = $"Imported '{instance.Name}' successfully";
                await LoadInstancesAsync();
            }
            else
            {
                StatusText = "Packwiz import failed — check log for details";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Packwiz import failed");
            StatusText = "Import failed";
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var update = await _updateChecker.CheckForUpdateAsync();
            if (update != null)
            {
                _logger.Information("Update available: {Version}", update.LatestVersion);
                StatusText = $"Update available: v{update.LatestVersion}!";

                // Show dialog notification on UI thread
                Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var closeBtn = new Avalonia.Controls.Button { Content = "Close", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
                        var msgBox = new Avalonia.Controls.Window
                        {
                            Title = "Update Available",
                            Width = 420,
                            Height = 200,
                            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                            CanResize = false,
                            Content = new Avalonia.Controls.StackPanel
                            {
                                Margin = new Avalonia.Thickness(20),
                                Spacing = 12,
                                Children =
                                {
                                    new Avalonia.Controls.TextBlock { Text = $"Obsidian Launcher v{update.LatestVersion} is available!", FontWeight = Avalonia.Media.FontWeight.Bold, FontSize = 16 },
                                    new Avalonia.Controls.TextBlock { Text = $"You are running v{update.CurrentVersion}.", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                                    new Avalonia.Controls.TextBlock { Text = update.ReleaseNotes.Length > 200 ? update.ReleaseNotes.Substring(0, 200) + "..." : update.ReleaseNotes, TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 12 },
                                    closeBtn
                                }
                            }
                        };
                        closeBtn.Click += (_, _) => msgBox.Close();
                        await msgBox.ShowDialog(desktop.MainWindow!);
                    }
                });
            }
            else
            {
                StatusText = "You are running the latest version.";
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Update check failed");
            StatusText = "Update check failed";
        }
    }

    private async Task LoadNewsAsync()
    {
        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "ObsidianLauncher/1.0");
            client.Timeout = TimeSpan.FromSeconds(10);
            var json = await client.GetStringAsync("https://launchercontent.mojang.com/v2/javaPatchNotes.json");
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("entries", out var entries) && entries.GetArrayLength() > 0)
            {
                var first = entries[0];
                var title = first.TryGetProperty("title", out var t) ? t.GetString() : null;
                if (!string.IsNullOrEmpty(title))
                    LatestNewsHeadline = title;
                else
                    LatestNewsHeadline = "Minecraft Java Edition — latest patch notes";
            }
            else
            {
                LatestNewsHeadline = "Minecraft Java Edition news";
            }
        }
        catch
        {
            LatestNewsHeadline = "Minecraft Java Edition news — click More News";
        }
    }

    private async Task OpenModBrowserAsync()
    {
        if (SelectedInstance == null) return;
        try
        {
            var win = new Views.ModBrowserWindow(_modManager, SelectedInstance);
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                await win.ShowDialog(desktop.MainWindow!);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open mod browser");
            StatusText = "Failed to open mod browser";
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

    private async void OpenBackupManager()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
            var win = new Views.BackupManagerWindow(Instances.ToList(), _backupManager, _instanceManager);
            await win.ShowDialog(desktop.MainWindow!);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open Backup Manager");
        }
    }

    // --- Create instance wizard helpers ---

    private bool CanAdvanceCreateStep()
    {
        if (CreateStep == 1) return true;
        if (CreateStep == 2) return !string.IsNullOrEmpty(CurrentCreateVm?.SelectedVersionId);
        return false;
    }

    private async Task AdvanceCreateStepAsync()
    {
        if (CreateStep == 1)
        {
            CurrentCreateVm = CreateNewInstanceVM();
            if (!string.IsNullOrWhiteSpace(NewInstanceName))
                CurrentCreateVm.InstanceName = NewInstanceName;
            CurrentCreateVm.Palette = NewInstancePalette;
            CurrentCreateVm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CreateInstanceViewModel.SelectedVersionId))
                    ((RelayCommand)AdvanceCreateStepCommand).RaiseCanExecuteChanged();
            };
            CreateStep = 2;
        }
        else if (CreateStep == 2 && !string.IsNullOrEmpty(CurrentCreateVm?.SelectedVersionId))
        {
            CreateStep = 3;
        }
        await Task.CompletedTask;
    }

    // --- Theme ---

    private void SetTheme(bool dark)
    {
        IsDarkMode = dark;
        _launcherSettings.Theme.Value = dark ? "dark" : "light";
        _launcherSettings.Save();
    }

    // --- Screenshots ---

    private async Task LoadScreenshotsAsync()
    {
        ScreenshotFiles.Clear();
        try
        {
            var instancesDir = _launcherConfig.InstancesRootDir;
            if (!Directory.Exists(instancesDir)) return;

            foreach (var instanceDir in Directory.GetDirectories(instancesDir))
            {
                var screenshotsDir = Path.Combine(instanceDir, ".minecraft", "screenshots");
                if (!Directory.Exists(screenshotsDir))
                {
                    screenshotsDir = Path.Combine(instanceDir, "screenshots");
                    if (!Directory.Exists(screenshotsDir)) continue;
                }

                foreach (var file in Directory.GetFiles(screenshotsDir, "*.png").OrderByDescending(f => f))
                {
                    var info = new FileInfo(file);
                    ScreenshotFiles.Add(new ScreenshotItem
                    {
                        FilePath = file,
                        FileName = Path.GetFileNameWithoutExtension(file),
                        DateTaken = info.LastWriteTime,
                        FileSizeBytes = info.Length
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load screenshots");
        }
    }

    private void OpenScreenshotsFolder()
    {
        string path;
        if (SelectedInstance != null)
        {
            var dir = Path.Combine(SelectedInstance.GameDataPath, "screenshots");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            path = dir;
        }
        else
        {
            path = _launcherConfig.InstancesRootDir;
        }
        OpenFolder(path);
    }
}

// Command that forwards the Execute parameter to an action
public class ParamRelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    public ParamRelayCommand(Action<object?> execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
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
