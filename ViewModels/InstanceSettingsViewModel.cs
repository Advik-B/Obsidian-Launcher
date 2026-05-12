// ViewModels/InstanceSettingsViewModel.cs

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ObsidianLauncher.Models;
using ObsidianLauncher.Settings;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class InstanceSettingsViewModel : INotifyPropertyChanged
{
    private readonly Instance _instance;
    private readonly LauncherSettings? _launcherSettings;
    private Settings.SettingsManager? _instanceConfig;
    private bool _hasUnsavedChanges;

    private string _name = "";
    private string _notes = "";
    private string _author = "";
    private bool _isFavorite;

    private string _javaPath = "";
    private int _minMemoryMB;
    private int _maxMemoryMB;
    private string _javaArgs = "";
    private bool _useCustomJavaSettings;

    private int _windowWidth;
    private int _windowHeight;
    private bool _fullscreen;
    private bool _useCustomGameSettings;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<Action<string?>>? BrowseJavaPathRequested;

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

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public string Notes
    {
        get => _notes;
        set { if (_notes != value) { _notes = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public string Author
    {
        get => _author;
        set { if (_author != value) { _author = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set { if (_isFavorite != value) { _isFavorite = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public bool UseCustomJavaSettings
    {
        get => _useCustomJavaSettings;
        set { if (_useCustomJavaSettings != value) { _useCustomJavaSettings = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public string JavaPath
    {
        get => _javaPath;
        set { if (_javaPath != value) { _javaPath = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public int MinMemoryMB
    {
        get => _minMemoryMB;
        set { if (_minMemoryMB != value) { _minMemoryMB = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public int MaxMemoryMB
    {
        get => _maxMemoryMB;
        set { if (_maxMemoryMB != value) { _maxMemoryMB = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public string JavaArgs
    {
        get => _javaArgs;
        set { if (_javaArgs != value) { _javaArgs = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public bool UseCustomGameSettings
    {
        get => _useCustomGameSettings;
        set { if (_useCustomGameSettings != value) { _useCustomGameSettings = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public int WindowWidth
    {
        get => _windowWidth;
        set { if (_windowWidth != value) { _windowWidth = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public int WindowHeight
    {
        get => _windowHeight;
        set { if (_windowHeight != value) { _windowHeight = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public bool Fullscreen
    {
        get => _fullscreen;
        set { if (_fullscreen != value) { _fullscreen = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public ICommand BrowseJavaPathCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    // Constructor for use without settings (e.g., designer)
    public InstanceSettingsViewModel(Instance instance) : this(instance, null) { }

    public InstanceSettingsViewModel(Instance instance, LauncherSettings? launcherSettings)
    {
        _instance = instance;
        _launcherSettings = launcherSettings;

        BrowseJavaPathCommand = new RelayCommand(BrowseJavaPath);
        SaveCommand = new RelayCommand(SaveSettings, () => HasUnsavedChanges);
        CancelCommand = new RelayCommand(() => { });

        LoadSettings();
        HasUnsavedChanges = false;
    }

    private void LoadSettings()
    {
        _name = _instance.Name;
        _notes = _instance.Notes ?? "";
        _author = _instance.Author ?? "";
        _isFavorite = _instance.IsFavorite;

        if (_launcherSettings != null && !string.IsNullOrEmpty(_instance.InstancePath))
        {
            var configPath = Path.Combine(_instance.InstancePath, "instance_config.toml");
            _instanceConfig = _launcherSettings.CreateInstanceSettings(configPath);

            _useCustomJavaSettings = _instanceConfig.RegisterBool("UseCustomJavaSettings", false).Value;
            _javaPath = _instanceConfig.RegisterString("JavaPath", "").Value;
            _minMemoryMB = _instanceConfig.RegisterInt("MinMemoryMB", 2048).Value;
            _maxMemoryMB = _instanceConfig.RegisterInt("MaxMemoryMB", 4096).Value;
            _javaArgs = _instanceConfig.RegisterString("JavaArgs", "").Value;

            _useCustomGameSettings = _instanceConfig.RegisterBool("UseCustomGameSettings", false).Value;
            _windowWidth = _instanceConfig.RegisterInt("WindowWidth", 1280).Value;
            _windowHeight = _instanceConfig.RegisterInt("WindowHeight", 720).Value;
            _fullscreen = _instanceConfig.RegisterBool("Fullscreen", false).Value;
        }
        else
        {
            _useCustomJavaSettings = false;
            _javaPath = "";
            _minMemoryMB = 2048;
            _maxMemoryMB = 4096;
            _javaArgs = "";
            _useCustomGameSettings = false;
            _windowWidth = 1280;
            _windowHeight = 720;
            _fullscreen = false;
        }

        OnPropertyChanged(string.Empty);
    }

    private void SaveSettings()
    {
        Log.Information("Saving instance settings for {Instance}", _instance.Name);

        _instance.Name = Name;
        _instance.Notes = Notes;
        _instance.Author = Author;
        _instance.IsFavorite = IsFavorite;

        if (_instanceConfig != null)
        {
            _instanceConfig.Get<bool>("UseCustomJavaSettings")!.Value = UseCustomJavaSettings;
            _instanceConfig.Get<string>("JavaPath")!.Value = JavaPath;
            _instanceConfig.Get<int>("MinMemoryMB")!.Value = MinMemoryMB;
            _instanceConfig.Get<int>("MaxMemoryMB")!.Value = MaxMemoryMB;
            _instanceConfig.Get<string>("JavaArgs")!.Value = JavaArgs;
            _instanceConfig.Get<bool>("UseCustomGameSettings")!.Value = UseCustomGameSettings;
            _instanceConfig.Get<int>("WindowWidth")!.Value = WindowWidth;
            _instanceConfig.Get<int>("WindowHeight")!.Value = WindowHeight;
            _instanceConfig.Get<bool>("Fullscreen")!.Value = Fullscreen;
            _instanceConfig.SaveAll();
        }

        HasUnsavedChanges = false;
        Log.Information("Instance settings saved for {Instance}", _instance.Name);
    }

    private void BrowseJavaPath()
    {
        BrowseJavaPathRequested?.Invoke(this, path =>
        {
            if (path != null) JavaPath = path;
        });
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
