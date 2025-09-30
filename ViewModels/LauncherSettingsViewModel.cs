using System;
using System.Reactive;
using ReactiveUI;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class LauncherSettingsViewModel : ViewModelBase
{
    private readonly ILogger _logger = Log.ForContext<LauncherSettingsViewModel>();
    private readonly LauncherConfig _launcherConfig;
    
    private string _javaPath = "";
    private int _memoryAllocation = 4096;
    private bool _autoUpdate = true;
    private string _gameDataPath = "";

    public ReactiveCommand<Unit, Unit> SaveSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetToDefaultsCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseJavaPathCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseGameDataPathCommand { get; }

    public string JavaPath
    {
        get => _javaPath;
        set => SetField(ref _javaPath, value);
    }

    public int MemoryAllocation
    {
        get => _memoryAllocation;
        set => SetField(ref _memoryAllocation, value);
    }

    public bool AutoUpdate
    {
        get => _autoUpdate;
        set => SetField(ref _autoUpdate, value);
    }

    public string GameDataPath
    {
        get => _gameDataPath;
        set => SetField(ref _gameDataPath, value);
    }

    public LauncherSettingsViewModel(LauncherConfig launcherConfig)
    {
        _launcherConfig = launcherConfig ?? throw new ArgumentNullException(nameof(launcherConfig));
        
        SaveSettingsCommand = ReactiveCommand.Create(SaveSettings);
        ResetToDefaultsCommand = ReactiveCommand.Create(ResetToDefaults);
        BrowseJavaPathCommand = ReactiveCommand.Create(BrowseJavaPath);
        BrowseGameDataPathCommand = ReactiveCommand.Create(BrowseGameDataPath);
        
        LoadSettings();
    }

    private void LoadSettings()
    {
        GameDataPath = _launcherConfig.BaseDataPath;
        // TODO: Load other settings from config file or registry
        _logger.Information("Loading launcher settings...");
    }

    private void SaveSettings()
    {
        // TODO: Save settings to config file or registry
        _logger.Information("Saving launcher settings...");
        _logger.Information("Java Path: {JavaPath}", JavaPath);
        _logger.Information("Memory Allocation: {MemoryAllocation} MB", MemoryAllocation);
        _logger.Information("Auto Update: {AutoUpdate}", AutoUpdate);
        _logger.Information("Game Data Path: {GameDataPath}", GameDataPath);
    }

    private void ResetToDefaults()
    {
        JavaPath = "";
        MemoryAllocation = 4096;
        AutoUpdate = true;
        GameDataPath = _launcherConfig.BaseDataPath;
        _logger.Information("Settings reset to defaults");
    }

    private void BrowseJavaPath()
    {
        // TODO: Open file dialog to select Java executable
        _logger.Information("Browse Java path clicked");
    }

    private void BrowseGameDataPath()
    {
        // TODO: Open folder dialog to select game data directory
        _logger.Information("Browse game data path clicked");
    }
}