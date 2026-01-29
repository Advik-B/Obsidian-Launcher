// ViewModels/InstanceSettingsViewModel.cs

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ObsidianLauncher.Models;
using Serilog;

namespace ObsidianLauncher.ViewModels;

/// <summary>
///     ViewModel for the Instance Settings dialog.
/// </summary>
public class InstanceSettingsViewModel : INotifyPropertyChanged
{
    private readonly Instance _instance;
    private bool _hasUnsavedChanges;

    // Instance metadata
    private string _name = "";
    private string _notes = "";
    private string _author = "";
    private bool _isFavorite;

    // Java settings (instance overrides)
    private string _javaPath = "";
    private int _minMemoryMB;
    private int _maxMemoryMB;
    private string _javaArgs = "";
    private bool _useCustomJavaSettings;

    // Game settings
    private int _windowWidth;
    private int _windowHeight;
    private bool _fullscreen;
    private bool _useCustomGameSettings;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Has unsaved changes.
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
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }
    }

    // Metadata Properties
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set
        {
            if (_notes != value)
            {
                _notes = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public string Author
    {
        get => _author;
        set
        {
            if (_author != value)
            {
                _author = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    // Java Settings Properties
    public bool UseCustomJavaSettings
    {
        get => _useCustomJavaSettings;
        set
        {
            if (_useCustomJavaSettings != value)
            {
                _useCustomJavaSettings = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

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
    public bool UseCustomGameSettings
    {
        get => _useCustomGameSettings;
        set
        {
            if (_useCustomGameSettings != value)
            {
                _useCustomGameSettings = value;
                HasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

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

    // Commands
    public ICommand BrowseJavaPathCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public InstanceSettingsViewModel(Instance instance)
    {
        _instance = instance;

        // Initialize commands
        BrowseJavaPathCommand = new RelayCommand(BrowseJavaPath);
        SaveCommand = new RelayCommand(SaveSettings, () => HasUnsavedChanges);
        CancelCommand = new RelayCommand(() => { }); // Dialog handles close

        // Load current values
        LoadSettings();
        HasUnsavedChanges = false;
    }

    private void LoadSettings()
    {
        // Load metadata
        _name = _instance.Name;
        _notes = _instance.Notes ?? "";
        _author = _instance.Author ?? "";
        _isFavorite = _instance.IsFavorite;

        // TODO: Load custom Java settings from instance config
        _useCustomJavaSettings = false;
        _javaPath = "";
        _minMemoryMB = 2048;
        _maxMemoryMB = 4096;
        _javaArgs = "";

        // TODO: Load custom game settings from instance config
        _useCustomGameSettings = false;
        _windowWidth = 1280;
        _windowHeight = 720;
        _fullscreen = false;

        OnPropertyChanged(string.Empty); // Notify all properties changed
    }

    private void SaveSettings()
    {
        Log.Information("Saving instance settings for {Instance}", _instance.Name);

        // Update instance metadata
        _instance.Name = Name;
        _instance.Notes = Notes;
        _instance.Author = Author;
        _instance.IsFavorite = IsFavorite;

        // TODO: Save custom Java settings to instance config file
        // TODO: Save custom game settings to instance config file

        HasUnsavedChanges = false;
        Log.Information("Instance settings saved");
    }

    private void BrowseJavaPath()
    {
        Log.Information("Browse Java path triggered");
        // TODO: Show file picker dialog
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
