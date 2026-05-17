// Settings/LauncherSettings.cs

using System;
using System.IO;

namespace ObsidianLauncher.Settings;

/// <summary>
///     Global launcher settings with hierarchical override support.
/// </summary>
public class LauncherSettings
{
    private readonly SettingsManager _settingsManager;

    // General Settings
    public Setting<string> Language { get; }
    public Setting<string> Theme { get; }
    public Setting<bool> CheckForUpdates { get; }
    public Setting<bool> ShowConsoleOnLaunch { get; }
    public Setting<bool> CloseAfterLaunch { get; }

    // Java Settings
    public Setting<string> JavaPath { get; }
    public Setting<int> MinMemoryMB { get; }
    public Setting<int> MaxMemoryMB { get; }
    public Setting<string> JavaArgs { get; }

    // Game Settings
    public Setting<int> WindowWidth { get; }
    public Setting<int> WindowHeight { get; }
    public Setting<bool> Fullscreen { get; }
    public Setting<string> GameDirectory { get; }

    // Network Settings
    public Setting<bool> UseProxy { get; }
    public Setting<string> ProxyHost { get; }
    public Setting<int> ProxyPort { get; }
    public Setting<int> MaxConcurrentDownloads { get; }

    // Launcher Behavior
    public Setting<bool> ShowSnapshots { get; }
    public Setting<bool> ShowOldAlpha { get; }
    public Setting<bool> ShowOldBeta { get; }
    public Setting<string> LastSelectedInstance { get; }
    public Setting<string> DefaultInstanceGroup { get; }
    public Setting<bool> FirstRunCompleted { get; }

    /// <summary>
    ///     Creates launcher settings from the specified configuration file.
    /// </summary>
    /// <param name="configFilePath">Path to the launcher configuration TOML file.</param>
    public LauncherSettings(string configFilePath)
    {
        _settingsManager = new SettingsManager(configFilePath, "General");

        // General Settings
        Language = _settingsManager.RegisterString("Language", "en-US", "UI language");
        Theme = _settingsManager.RegisterString("Theme", "dark", "UI theme (dark, light, system)");
        CheckForUpdates = _settingsManager.RegisterBool("CheckForUpdates", true, "Check for launcher updates");
        ShowConsoleOnLaunch = _settingsManager.RegisterBool("ShowConsoleOnLaunch", true, "Show console window when game launches");
        CloseAfterLaunch = _settingsManager.RegisterBool("CloseAfterLaunch", false, "Close launcher after game starts");

        // Java Settings
        JavaPath = _settingsManager.RegisterString("JavaPath", "", "Custom Java executable path (empty for auto-detect)");
        MinMemoryMB = _settingsManager.RegisterInt("MinMemoryMB", 512, "Minimum JVM memory in MB");
        MaxMemoryMB = _settingsManager.RegisterInt("MaxMemoryMB", 2048, "Maximum JVM memory in MB");
        JavaArgs = _settingsManager.RegisterString("JavaArgs", "", "Additional JVM arguments");

        // Game Settings
        WindowWidth = _settingsManager.RegisterInt("WindowWidth", 1280, "Game window width");
        WindowHeight = _settingsManager.RegisterInt("WindowHeight", 720, "Game window height");
        Fullscreen = _settingsManager.RegisterBool("Fullscreen", false, "Start game in fullscreen");
        GameDirectory = _settingsManager.RegisterString("GameDirectory", "", "Custom game directory (empty for default)");

        // Network Settings
        UseProxy = _settingsManager.RegisterBool("UseProxy", false, "Use proxy for downloads");
        ProxyHost = _settingsManager.RegisterString("ProxyHost", "", "Proxy server hostname");
        ProxyPort = _settingsManager.RegisterInt("ProxyPort", 8080, "Proxy server port");
        MaxConcurrentDownloads = _settingsManager.RegisterInt("MaxConcurrentDownloads", 5, "Maximum concurrent downloads");

        // Launcher Behavior
        ShowSnapshots = _settingsManager.RegisterBool("ShowSnapshots", false, "Show snapshot versions");
        ShowOldAlpha = _settingsManager.RegisterBool("ShowOldAlpha", false, "Show old alpha versions");
        ShowOldBeta = _settingsManager.RegisterBool("ShowOldBeta", false, "Show old beta versions");
        LastSelectedInstance = _settingsManager.RegisterString("LastSelectedInstance", "", "Last selected instance ID");
        DefaultInstanceGroup = _settingsManager.RegisterString("DefaultInstanceGroup", "Default", "Default instance group");
        FirstRunCompleted = _settingsManager.RegisterBool("FirstRunCompleted", false, "Setup wizard has been completed");
    }

    /// <summary>
    ///     Creates a child settings manager for instance-specific overrides.
    /// </summary>
    /// <param name="instanceConfigPath">Path to the instance configuration file.</param>
    /// <returns>A new SettingsManager with this as the parent.</returns>
    public SettingsManager CreateInstanceSettings(string instanceConfigPath)
    {
        return new SettingsManager(instanceConfigPath, "Instance", _settingsManager);
    }

    /// <summary>
    ///     Saves all settings to disk.
    /// </summary>
    public void Save()
    {
        _settingsManager.SaveAll();
    }

    /// <summary>
    ///     Reloads all settings from disk.
    /// </summary>
    public void Reload()
    {
        _settingsManager.Reload();
    }

    /// <summary>
    ///     Resets a specific setting to its default value.
    /// </summary>
    /// <param name="key">Setting key name.</param>
    public void Reset(string key)
    {
        _settingsManager.Reset(key);
    }

    /// <summary>
    ///     Gets the effective Java path, using auto-detection if not set.
    /// </summary>
    /// <returns>Java executable path.</returns>
    public string GetEffectiveJavaPath()
    {
        if (!string.IsNullOrWhiteSpace(JavaPath.Value) && File.Exists(JavaPath.Value))
        {
            return JavaPath.Value;
        }

        // Return empty to trigger auto-detection
        return string.Empty;
    }

    /// <summary>
    ///     Gets the effective game directory, using default if not set.
    /// </summary>
    /// <param name="defaultDirectory">Default game directory.</param>
    /// <returns>Game directory path.</returns>
    public string GetEffectiveGameDirectory(string defaultDirectory)
    {
        if (!string.IsNullOrWhiteSpace(GameDirectory.Value) && Directory.Exists(GameDirectory.Value))
        {
            return GameDirectory.Value;
        }

        return defaultDirectory;
    }

    /// <summary>
    ///     Validates memory settings to ensure they're within reasonable bounds.
    /// </summary>
    /// <returns>True if settings are valid, false otherwise.</returns>
    public bool ValidateMemorySettings()
    {
        const int minAllowed = 256;  // 256 MB minimum
        const int maxAllowed = 32768; // 32 GB maximum

        if (MinMemoryMB.Value < minAllowed || MinMemoryMB.Value > maxAllowed)
        {
            MinMemoryMB.Value = Math.Clamp(MinMemoryMB.Value, minAllowed, maxAllowed);
            return false;
        }

        if (MaxMemoryMB.Value < minAllowed || MaxMemoryMB.Value > maxAllowed)
        {
            MaxMemoryMB.Value = Math.Clamp(MaxMemoryMB.Value, minAllowed, maxAllowed);
            return false;
        }

        if (MinMemoryMB.Value > MaxMemoryMB.Value)
        {
            // Swap them
            (MinMemoryMB.Value, MaxMemoryMB.Value) = (MaxMemoryMB.Value, MinMemoryMB.Value);
            return false;
        }

        return true;
    }
}
