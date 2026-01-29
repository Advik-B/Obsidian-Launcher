// ViewModels/SettingsViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ObsidianLauncher.Settings;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.ViewModels;

/// <summary>
///     ViewModel for the Settings dialog with multi-page support.
/// </summary>
public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly LauncherSettings _settings;
    private int _selectedPageIndex;
    private bool _hasUnsavedChanges;

    public event PropertyChangedEventHandler? PropertyChanged;

    // General Settings
    private string _language = "";
    private string _theme = "";
    private bool _checkForUpdates;
    private bool _showConsoleOnLaunch;
    private bool _closeAfterLaunch;

    // Java Settings
    private string _javaPath = "";
    private int _minMemoryMB;
    private int _maxMemoryMB;
    private string _javaArgs = "";

    // Game Settings
    private int _windowWidth;
    private int _windowHeight;
    private bool _fullscreen;
    private string _gameDirectory = "";

    // Network Settings
    private bool _useProxy;
    private string _proxyHost = "";
    private int _proxyPort;
    private int _maxConcurrentDownloads;

    // Launcher Behavior
    private bool _showSnapshots;
    private bool _showOldAlpha;
    private bool _showOldBeta;
    private string _defaultInstanceGroup = "";

    /// <summary>
    ///     Available UI themes.
    /// </summary>
    public ObservableCollection<string> AvailableThemes { get; }

    /// <summary>
    ///     Available languages.
    /// </summary>
    public ObservableCollection<string> AvailableLanguages { get; }

    /// <summary>
    ///     Current selected settings page index.
    /// </summary>
    public int SelectedPageIndex
    {
        get => _selectedPageIndex;
        set
        {
            if (_selectedPageIndex != value)
            {
                _selectedPageIndex = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    ///     Indicates if there are unsaved changes.
    /// </summary>
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set
        {
            if (_hasUnsavedChanges != value)
            {
                _hasUnsavedChanges = value;
                OnPropertyChanged();
                // Notify Save command that CanExecute has changed
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }
    }

    // General Settings Properties
    public string Language
    {
        get => _language;
        set
        {
            if (_language != value)
            {
                _language = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public string Theme
    {
        get => _theme;
        set
        {
            if (_theme != value)
            {
                _theme = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public bool CheckForUpdates
    {
        get => _checkForUpdates;
        set
        {
            if (_checkForUpdates != value)
            {
                _checkForUpdates = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowConsoleOnLaunch
    {
        get => _showConsoleOnLaunch;
        set
        {
            if (_showConsoleOnLaunch != value)
            {
                _showConsoleOnLaunch = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public bool CloseAfterLaunch
    {
        get => _closeAfterLaunch;
        set
        {
            if (_closeAfterLaunch != value)
            {
                _closeAfterLaunch = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    // Java Settings Properties
    public string JavaPath
    {
        get => _javaPath;
        set
        {
            if (_javaPath != value)
            {
                _javaPath = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public int MinMemoryMB
    {
        get => _minMemoryMB;
        set
        {
            if (_minMemoryMB != value)
            {
                _minMemoryMB = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public int MaxMemoryMB
    {
        get => _maxMemoryMB;
        set
        {
            if (_maxMemoryMB != value)
            {
                _maxMemoryMB = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public string JavaArgs
    {
        get => _javaArgs;
        set
        {
            if (_javaArgs != value)
            {
                _javaArgs = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    // Game Settings Properties
    public int WindowWidth
    {
        get => _windowWidth;
        set
        {
            if (_windowWidth != value)
            {
                _windowWidth = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public int WindowHeight
    {
        get => _windowHeight;
        set
        {
            if (_windowHeight != value)
            {
                _windowHeight = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public bool Fullscreen
    {
        get => _fullscreen;
        set
        {
            if (_fullscreen != value)
            {
                _fullscreen = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public string GameDirectory
    {
        get => _gameDirectory;
        set
        {
            if (_gameDirectory != value)
            {
                _gameDirectory = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    // Network Settings Properties
    public bool UseProxy
    {
        get => _useProxy;
        set
        {
            if (_useProxy != value)
            {
                _useProxy = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public string ProxyHost
    {
        get => _proxyHost;
        set
        {
            if (_proxyHost != value)
            {
                _proxyHost = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public int ProxyPort
    {
        get => _proxyPort;
        set
        {
            if (_proxyPort != value)
            {
                _proxyPort = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public int MaxConcurrentDownloads
    {
        get => _maxConcurrentDownloads;
        set
        {
            if (_maxConcurrentDownloads != value)
            {
                _maxConcurrentDownloads = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    // Launcher Behavior Properties
    public bool ShowSnapshots
    {
        get => _showSnapshots;
        set
        {
            if (_showSnapshots != value)
            {
                _showSnapshots = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowOldAlpha
    {
        get => _showOldAlpha;
        set
        {
            if (_showOldAlpha != value)
            {
                _showOldAlpha = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowOldBeta
    {
        get => _showOldBeta;
        set
        {
            if (_showOldBeta != value)
            {
                _showOldBeta = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public string DefaultInstanceGroup
    {
        get => _defaultInstanceGroup;
        set
        {
            if (_defaultInstanceGroup != value)
            {
                _defaultInstanceGroup = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    // Commands
    public ICommand BrowseJavaPathCommand { get; }
    public ICommand BrowseGameDirectoryCommand { get; }
    public ICommand ResetToDefaultsCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public SettingsViewModel(LauncherSettings settings)
    {
        _settings = settings;

        // Initialize collections
        AvailableThemes = new ObservableCollection<string> { "dark", "light", "system" };  // Match LauncherSettings defaults
        AvailableLanguages = new ObservableCollection<string> { "en-US", "en-GB", "de-DE", "fr-FR", "es-ES", "ja-JP", "zh-CN" };

        // Load current values from settings
        LoadSettings();

        // Initialize commands
        BrowseJavaPathCommand = new RelayCommand(BrowseJavaPath);
        BrowseGameDirectoryCommand = new RelayCommand(BrowseGameDirectory);
        ResetToDefaultsCommand = new RelayCommand(ResetToDefaults);
        SaveCommand = new RelayCommand(SaveSettings, () => HasUnsavedChanges);
        CancelCommand = new RelayCommand(CancelChanges);

        HasUnsavedChanges = false; // Reset after initial load
    }

    private void LoadSettings()
    {
        // Use backing fields to avoid triggering property change notifications
        _language = _settings.Language.Value;
        _theme = _settings.Theme.Value;
        _checkForUpdates = _settings.CheckForUpdates.Value;
        _showConsoleOnLaunch = _settings.ShowConsoleOnLaunch.Value;
        _closeAfterLaunch = _settings.CloseAfterLaunch.Value;

        _javaPath = _settings.JavaPath.Value;
        _minMemoryMB = _settings.MinMemoryMB.Value;
        _maxMemoryMB = _settings.MaxMemoryMB.Value;
        _javaArgs = _settings.JavaArgs.Value;

        _windowWidth = _settings.WindowWidth.Value;
        _windowHeight = _settings.WindowHeight.Value;
        _fullscreen = _settings.Fullscreen.Value;
        _gameDirectory = _settings.GameDirectory.Value;

        _useProxy = _settings.UseProxy.Value;
        _proxyHost = _settings.ProxyHost.Value;
        _proxyPort = _settings.ProxyPort.Value;
        _maxConcurrentDownloads = _settings.MaxConcurrentDownloads.Value;

        _showSnapshots = _settings.ShowSnapshots.Value;
        _showOldAlpha = _settings.ShowOldAlpha.Value;
        _showOldBeta = _settings.ShowOldBeta.Value;
        _defaultInstanceGroup = _settings.DefaultInstanceGroup.Value;

        // Notify all properties changed at once
        OnPropertyChanged(string.Empty);
    }

    private void SaveSettings()
    {
        try
        {
            // General
            _settings.Language.Value = Language;
            _settings.Theme.Value = Theme;
            _settings.CheckForUpdates.Value = CheckForUpdates;
            _settings.ShowConsoleOnLaunch.Value = ShowConsoleOnLaunch;
            _settings.CloseAfterLaunch.Value = CloseAfterLaunch;

            // Java
            _settings.JavaPath.Value = JavaPath;
            _settings.MinMemoryMB.Value = MinMemoryMB;
            _settings.MaxMemoryMB.Value = MaxMemoryMB;
            _settings.JavaArgs.Value = JavaArgs;

            // Game
            _settings.WindowWidth.Value = WindowWidth;
            _settings.WindowHeight.Value = WindowHeight;
            _settings.Fullscreen.Value = Fullscreen;
            _settings.GameDirectory.Value = GameDirectory;

            // Network
            _settings.UseProxy.Value = UseProxy;
            _settings.ProxyHost.Value = ProxyHost;
            _settings.ProxyPort.Value = ProxyPort;
            _settings.MaxConcurrentDownloads.Value = MaxConcurrentDownloads;

            // Behavior
            _settings.ShowSnapshots.Value = ShowSnapshots;
            _settings.ShowOldAlpha.Value = ShowOldAlpha;
            _settings.ShowOldBeta.Value = ShowOldBeta;
            _settings.DefaultInstanceGroup.Value = DefaultInstanceGroup;

            // Validate and save
            _settings.ValidateMemorySettings();
            _settings.Save();

            HasUnsavedChanges = false;

            Log.Information("Settings saved successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings");
        }
    }

    private void CancelChanges()
    {
        LoadSettings();
        HasUnsavedChanges = false;
    }

    private void ResetToDefaults()
    {
        // Reset each setting to default
        var keys = new[]
        {
            "Language", "Theme", "CheckForUpdates", "ShowConsoleOnLaunch", "CloseAfterLaunch",
            "JavaPath", "MinMemoryMB", "MaxMemoryMB", "JavaArgs",
            "WindowWidth", "WindowHeight", "Fullscreen", "GameDirectory",
            "UseProxy", "ProxyHost", "ProxyPort", "MaxConcurrentDownloads",
            "ShowSnapshots", "ShowOldAlpha", "ShowOldBeta", "DefaultInstanceGroup"
        };

        foreach (var key in keys)
        {
            _settings.Reset(key);
        }

        LoadSettings();
        HasUnsavedChanges = true;
    }

    private void BrowseJavaPath()
    {
        // TODO: Implement file picker dialog when needed
        // For now, just log
        Log.Information("Browse Java path requested");
    }

    private void BrowseGameDirectory()
    {
        // TODO: Implement folder picker dialog when needed
        // For now, just log
        Log.Information("Browse game directory requested");
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
