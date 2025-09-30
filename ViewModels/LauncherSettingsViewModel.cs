using System;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class LauncherSettingsViewModel : ViewModelBase
{
    private readonly ILogger _logger = Log.ForContext<LauncherSettingsViewModel>();
    
    private string _javaPath = "";
    private int _memoryAllocation = 4096;
    private bool _autoUpdate = true;
    private string _gameDataPath = "";

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

    public LauncherSettingsViewModel()
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        // TODO: Load settings from LauncherConfig
        _logger.Information("Loading launcher settings...");
    }

    public void SaveSettings()
    {
        // TODO: Save settings to LauncherConfig
        _logger.Information("Saving launcher settings...");
    }
}