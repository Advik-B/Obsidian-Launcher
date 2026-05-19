// ViewModels/InstanceSettingsViewModel.cs

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
    private string _customIconPath = "";

    private string _javaPath = "";
    private int _minMemoryMB;
    private int _maxMemoryMB;
    private string _javaArgs = "";
    private bool _useCustomJavaSettings;

    private int _windowWidth;
    private int _windowHeight;
    private bool _fullscreen;
    private bool _useCustomGameSettings;

    // Launch pipeline
    private string _preLaunchCommand = "";
    private string _postLaunchCommand = "";
    private string _wrapperCommand = "";
    private string _quickPlayServer = "";
    private string _quickPlayWorld = "";
    private EnvVarEntry? _selectedEnvVar;

    // Mods
    private ResourceItem? _selectedMod;
    private string _modsStatusText = "";

    // Other Logs
    private string? _selectedLogFile;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<Action<string?>>? BrowseJavaPathRequested;
    public event EventHandler<Action<string?>>? PickIconRequested;
    public event EventHandler? OpenScreenshotViewerRequested;
    public event EventHandler? OpenWorldManagerRequested;

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

    public string CustomIconPath
    {
        get => _customIconPath;
        set { if (_customIconPath != value) { _customIconPath = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
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

    // Launch pipeline properties
    public string PreLaunchCommand
    {
        get => _preLaunchCommand;
        set { if (_preLaunchCommand != value) { _preLaunchCommand = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public string PostLaunchCommand
    {
        get => _postLaunchCommand;
        set { if (_postLaunchCommand != value) { _postLaunchCommand = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public string WrapperCommand
    {
        get => _wrapperCommand;
        set { if (_wrapperCommand != value) { _wrapperCommand = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public string QuickPlayServer
    {
        get => _quickPlayServer;
        set { if (_quickPlayServer != value) { _quickPlayServer = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public string QuickPlayWorld
    {
        get => _quickPlayWorld;
        set { if (_quickPlayWorld != value) { _quickPlayWorld = value; HasUnsavedChanges = true; OnPropertyChanged(); } }
    }

    public ObservableCollection<EnvVarEntry> EnvironmentVariables { get; } = new();

    public EnvVarEntry? SelectedEnvVar
    {
        get => _selectedEnvVar;
        set
        {
            if (_selectedEnvVar != value)
            {
                _selectedEnvVar = value;
                OnPropertyChanged();
                ((RelayCommand)RemoveEnvVarCommand).RaiseCanExecuteChanged();
            }
        }
    }

    // Version display
    public string VersionDisplayText
    {
        get
        {
            var parts = new List<string>();
            foreach (var c in _instance.Components)
            {
                if (c.Uid == "net.minecraft" || c.Uid == "minecraft")
                    parts.Add($"Minecraft {c.Version}");
                else if (c.Uid.Contains("fabric"))
                    parts.Add($"Fabric {c.Version}");
                else if (c.Uid.Contains("forge"))
                    parts.Add($"Forge {c.Version}");
                else if (c.Uid.Contains("quilt"))
                    parts.Add($"Quilt {c.Version}");
                else if (c.Uid.Contains("neoforge"))
                    parts.Add($"NeoForge {c.Version}");
                else
                    parts.Add($"{c.Uid} {c.Version}");
            }
            return parts.Count > 0 ? string.Join(" + ", parts) : "Unknown version";
        }
    }

    // Mods
    public ObservableCollection<ResourceItem> Mods { get; } = new();

    public ResourceItem? SelectedMod
    {
        get => _selectedMod;
        set
        {
            if (_selectedMod != value)
            {
                _selectedMod = value;
                OnPropertyChanged();
                ((RelayCommand)ToggleModCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteModCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string ModsStatusText
    {
        get => _modsStatusText;
        set { _modsStatusText = value; OnPropertyChanged(); }
    }

    // Resource Packs
    public ObservableCollection<ResourceItem> ResourcePacks { get; } = new();

    // Shader Packs
    public ObservableCollection<ResourceItem> ShaderPacks { get; } = new();

    // Log Files
    public ObservableCollection<string> LogFiles { get; } = new();

    public string? SelectedLogFile
    {
        get => _selectedLogFile;
        set
        {
            if (_selectedLogFile != value)
            {
                _selectedLogFile = value;
                OnPropertyChanged();
                ((RelayCommand)OpenLogFileCommand).RaiseCanExecuteChanged();
            }
        }
    }

    // Commands
    public ICommand BrowseJavaPathCommand { get; }
    public ICommand PickIconCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AddEnvVarCommand { get; }
    public ICommand RemoveEnvVarCommand { get; }
    public ICommand LoadModsCommand { get; }
    public ICommand ToggleModCommand { get; }
    public ICommand ToggleModByItemCommand { get; }
    public ICommand OpenModsFolderCommand { get; }
    public ICommand DeleteModCommand { get; }
    public ICommand DeleteModByItemCommand { get; }
    public ICommand LoadResourcePacksCommand { get; }
    public ICommand OpenResourcePacksFolderCommand { get; }
    public ICommand LoadShaderPacksCommand { get; }
    public ICommand OpenShaderPacksFolderCommand { get; }
    public ICommand OpenScreenshotViewerCommand { get; }
    public ICommand OpenWorldManagerCommand { get; }
    public ICommand OpenInstanceFolderCommand { get; }
    public ICommand OpenLogFileCommand { get; }
    public ICommand RefreshLogFilesCommand { get; }

    // Constructor for use without settings (e.g., designer)
    public InstanceSettingsViewModel(Instance instance) : this(instance, null) { }


    public InstanceSettingsViewModel(Instance instance, LauncherSettings? launcherSettings)
    {
        _instance = instance;
        _launcherSettings = launcherSettings;

        BrowseJavaPathCommand = new RelayCommand(BrowseJavaPath);
        PickIconCommand = new RelayCommand(PickIcon);
        SaveCommand = new RelayCommand(SaveSettings, () => HasUnsavedChanges);
        CancelCommand = new RelayCommand(() => { }); // Dialog handles close
        AddEnvVarCommand = new RelayCommand(() => { EnvironmentVariables.Add(new EnvVarEntry()); HasUnsavedChanges = true; });
        RemoveEnvVarCommand = new RelayCommand(() => { if (SelectedEnvVar != null) { EnvironmentVariables.Remove(SelectedEnvVar); SelectedEnvVar = null; HasUnsavedChanges = true; } }, () => SelectedEnvVar != null);

        LoadModsCommand = new RelayCommand(LoadMods);
        ToggleModCommand = new RelayCommand(ToggleMod, () => SelectedMod != null);
        ToggleModByItemCommand = new ParamRelayCommand(obj => { if (obj is ResourceItem i) { SelectedMod = i; ToggleMod(); } });
        OpenModsFolderCommand = new RelayCommand(OpenModsFolder);
        DeleteModCommand = new RelayCommand(DeleteMod, () => SelectedMod != null);
        DeleteModByItemCommand = new ParamRelayCommand(obj => { if (obj is ResourceItem i) { SelectedMod = i; DeleteMod(); } });

        LoadResourcePacksCommand = new RelayCommand(LoadResourcePacks);
        OpenResourcePacksFolderCommand = new RelayCommand(OpenResourcePacksFolder);

        LoadShaderPacksCommand = new RelayCommand(LoadShaderPacks);
        OpenShaderPacksFolderCommand = new RelayCommand(OpenShaderPacksFolder);

        OpenScreenshotViewerCommand = new RelayCommand(() => OpenScreenshotViewerRequested?.Invoke(this, EventArgs.Empty));
        OpenWorldManagerCommand = new RelayCommand(() => OpenWorldManagerRequested?.Invoke(this, EventArgs.Empty));

        OpenInstanceFolderCommand = new RelayCommand(OpenInstanceFolder);
        OpenLogFileCommand = new RelayCommand(OpenLogFile, () => SelectedLogFile != null);
        RefreshLogFilesCommand = new RelayCommand(RefreshLogFiles);

        LoadSettings();
        HasUnsavedChanges = false;

        LoadMods();
        LoadResourcePacks();
        LoadShaderPacks();
        RefreshLogFiles();
    }

    private void LoadSettings()
    {
        _name = _instance.Name;
        _notes = _instance.Notes ?? "";
        _author = _instance.Author ?? "";
        _isFavorite = _instance.IsFavorite;
        _customIconPath = _instance.CustomIconPath ?? "";

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
        
        // Load launch pipeline
        _preLaunchCommand = _instance.PreLaunchCommand ?? "";
        _postLaunchCommand = _instance.PostLaunchCommand ?? "";
        _wrapperCommand = _instance.WrapperCommand ?? "";
        _quickPlayServer = _instance.QuickPlayServer ?? "";
        _quickPlayWorld = _instance.QuickPlayWorld ?? "";
        EnvironmentVariables.Clear();
        foreach (var kv in _instance.EnvironmentVariables)
            EnvironmentVariables.Add(new EnvVarEntry { Key = kv.Key, Value = kv.Value });

        OnPropertyChanged(string.Empty); // Notify all properties changed
    }

    private void SaveSettings()
    {
        Log.Information("Saving instance settings for {Instance}", _instance.Name);

        _instance.Name = Name;
        _instance.Notes = Notes;
        _instance.Author = Author;
        _instance.IsFavorite = IsFavorite;
        _instance.CustomIconPath = CustomIconPath;

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

        // Save launch pipeline
        _instance.PreLaunchCommand = string.IsNullOrWhiteSpace(PreLaunchCommand) ? null : PreLaunchCommand;
        _instance.PostLaunchCommand = string.IsNullOrWhiteSpace(PostLaunchCommand) ? null : PostLaunchCommand;
        _instance.WrapperCommand = string.IsNullOrWhiteSpace(WrapperCommand) ? null : WrapperCommand;
        _instance.QuickPlayServer = string.IsNullOrWhiteSpace(QuickPlayServer) ? null : QuickPlayServer;
        _instance.QuickPlayWorld = string.IsNullOrWhiteSpace(QuickPlayWorld) ? null : QuickPlayWorld;
        _instance.EnvironmentVariables.Clear();
        foreach (var entry in EnvironmentVariables)
            if (!string.IsNullOrWhiteSpace(entry.Key))
                _instance.EnvironmentVariables[entry.Key] = entry.Value ?? "";

        HasUnsavedChanges = false;
        Log.Information("Instance settings saved for {Instance}", _instance.Name);
    }

    private void LoadMods()
    {
        Mods.Clear();
        var modsFolder = System.IO.Path.Combine(_instance.GameDataPath, "mods");
        if (!Directory.Exists(modsFolder))
        {
            ModsStatusText = "No mods folder found.";
            return;
        }

        var files = Directory.GetFiles(modsFolder, "*.*");
        int count = 0;
        foreach (var file in files)
        {
            var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            // Match .jar, .zip, .jar.disabled
            if (ext == ".jar" || ext == ".zip" || file.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = System.IO.Path.GetFileName(file);
                bool isEnabled = !file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                // Strip .disabled suffix for display name
                var displayName = isEnabled
                    ? System.IO.Path.GetFileNameWithoutExtension(file)
                    : System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetFileNameWithoutExtension(file));
                var size = new FileInfo(file).Length;

                Mods.Add(new ResourceItem(file, displayName)
                {
                    IsEnabled = isEnabled,
                    SizeBytes = size
                });
                count++;
            }
        }

        ModsStatusText = count == 0 ? "No mods found." : $"{count} mod(s) loaded.";
    }

    private void ToggleMod()
    {
        if (SelectedMod == null) return;

        try
        {
            var path = SelectedMod.Path;
            string newPath;
            if (SelectedMod.IsEnabled)
                newPath = path + ".disabled";
            else
                newPath = path.Substring(0, path.Length - ".disabled".Length);

            File.Move(path, newPath);
            LoadMods();
        }
        catch (Exception ex)
        {
            ModsStatusText = $"Error toggling mod: {ex.Message}";
            Log.Error(ex, "Failed to toggle mod {Mod}", SelectedMod?.Name);
        }
    }

    private void OpenModsFolder()
    {
        var modsFolder = System.IO.Path.Combine(_instance.GameDataPath, "mods");
        if (!Directory.Exists(modsFolder))
            Directory.CreateDirectory(modsFolder);

        Process.Start(new ProcessStartInfo { FileName = modsFolder, UseShellExecute = true });
    }

    private void DeleteMod()
    {
        if (SelectedMod == null) return;

        try
        {
            ModsStatusText = $"Deleting {SelectedMod.Name}...";
            File.Delete(SelectedMod.Path);
            LoadMods();
            ModsStatusText = "Mod deleted.";
        }
        catch (Exception ex)
        {
            ModsStatusText = $"Error deleting mod: {ex.Message}";
            Log.Error(ex, "Failed to delete mod {Mod}", SelectedMod?.Name);
        }
    }

    private void LoadResourcePacks()
    {
        ResourcePacks.Clear();
        var folder = System.IO.Path.Combine(_instance.GameDataPath, "resourcepacks");
        if (!Directory.Exists(folder)) return;

        foreach (var file in Directory.GetFiles(folder))
        {
            var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".zip" || ext == ".jar")
            {
                ResourcePacks.Add(new ResourceItem(file, System.IO.Path.GetFileNameWithoutExtension(file))
                {
                    SizeBytes = new FileInfo(file).Length
                });
            }
        }
        // Also include subdirectories as resource packs
        foreach (var dir in Directory.GetDirectories(folder))
        {
            ResourcePacks.Add(new ResourceItem(dir, System.IO.Path.GetFileName(dir)));
        }
    }

    private void OpenResourcePacksFolder()
    {
        var folder = System.IO.Path.Combine(_instance.GameDataPath, "resourcepacks");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
    }

    private void LoadShaderPacks()
    {
        ShaderPacks.Clear();
        var folder = System.IO.Path.Combine(_instance.GameDataPath, "shaderpacks");
        if (!Directory.Exists(folder)) return;

        foreach (var file in Directory.GetFiles(folder))
        {
            var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".zip" || ext == ".jar")
            {
                ShaderPacks.Add(new ResourceItem(file, System.IO.Path.GetFileNameWithoutExtension(file))
                {
                    SizeBytes = new FileInfo(file).Length
                });
            }
        }
        foreach (var dir in Directory.GetDirectories(folder))
        {
            ShaderPacks.Add(new ResourceItem(dir, System.IO.Path.GetFileName(dir)));
        }
    }

    private void OpenShaderPacksFolder()
    {
        var folder = System.IO.Path.Combine(_instance.GameDataPath, "shaderpacks");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
    }

    private void OpenInstanceFolder()
    {
        var path = _instance.GameDataPath;
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void RefreshLogFiles()
    {
        LogFiles.Clear();
        var logsFolder = System.IO.Path.Combine(_instance.GameDataPath, "logs");
        if (!Directory.Exists(logsFolder)) return;

        foreach (var file in Directory.GetFiles(logsFolder))
        {
            LogFiles.Add(System.IO.Path.GetFileName(file));
        }
    }

    private void OpenLogFile()
    {
        if (SelectedLogFile == null) return;

        var filePath = System.IO.Path.Combine(_instance.GameDataPath, "logs", SelectedLogFile);
        if (!File.Exists(filePath)) return;

        try
        {
            Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open log file {FilePath}", filePath);
        }
    }

    private void PickIcon()
    {
        PickIconRequested?.Invoke(this, path =>
        {
            if (path != null) CustomIconPath = path;
        });
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

public class EnvVarEntry : INotifyPropertyChanged
{
    private string _key = "";
    private string _value = "";

    public string Key
    {
        get => _key;
        set { _key = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Key))); }
    }

    public string Value
    {
        get => _value;
        set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
