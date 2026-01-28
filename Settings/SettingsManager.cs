// Settings/SettingsManager.cs

using System;
using System.Collections.Generic;
using System.IO;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Settings;

/// <summary>
///     Manages hierarchical settings with support for global and per-instance overrides.
///     Settings are persisted to INI files.
/// </summary>
public class SettingsManager
{
    private static readonly ILogger _logger = Log.ForContext(typeof(SettingsManager));
    
    private readonly IniFile _iniFile;
    private readonly SettingsManager _parent;
    private readonly Dictionary<string, object> _settings;
    private readonly string _section;
    private readonly string _configFilePath;

    /// <summary>
    ///     Creates a new settings manager.
    /// </summary>
    /// <param name="configFilePath">Path to the INI configuration file.</param>
    /// <param name="section">Section name in the INI file (empty for default section).</param>
    /// <param name="parent">Parent settings manager for hierarchical overrides (null for global settings).</param>
    public SettingsManager(string configFilePath, string section = "", SettingsManager parent = null)
    {
        _configFilePath = configFilePath;
        _iniFile = new IniFile(configFilePath);
        _section = section ?? string.Empty;
        _parent = parent;
        _settings = new Dictionary<string, object>();
        
        _logger.Information("Created settings manager for {ConfigPath} (section: {Section})", 
            configFilePath, string.IsNullOrEmpty(_section) ? "<default>" : _section);
    }

    /// <summary>
    ///     Registers a string setting.
    /// </summary>
    /// <param name="key">Setting key name.</param>
    /// <param name="defaultValue">Default value.</param>
    /// <param name="description">Setting description.</param>
    /// <returns>The registered setting.</returns>
    public Setting<string> RegisterString(string key, string defaultValue = "", string description = "")
    {
        var setting = new Setting<string>(key, defaultValue, description);
        _settings[key] = setting;
        
        // Load value from INI file or parent
        var value = GetEffectiveValue(key, defaultValue);
        setting.SetValueSilently(value);
        
        // Subscribe to changes to auto-save
        setting.ValueChanged += (sender, e) => SaveSetting(key, e.NewValue);
        
        return setting;
    }

    /// <summary>
    ///     Registers an integer setting.
    /// </summary>
    /// <param name="key">Setting key name.</param>
    /// <param name="defaultValue">Default value.</param>
    /// <param name="description">Setting description.</param>
    /// <returns>The registered setting.</returns>
    public Setting<int> RegisterInt(string key, int defaultValue = 0, string description = "")
    {
        var setting = new Setting<int>(key, defaultValue, description);
        _settings[key] = setting;
        
        var value = GetEffectiveValue(key, defaultValue);
        setting.SetValueSilently(value);
        
        setting.ValueChanged += (sender, e) => SaveSetting(key, e.NewValue);
        
        return setting;
    }

    /// <summary>
    ///     Registers a boolean setting.
    /// </summary>
    /// <param name="key">Setting key name.</param>
    /// <param name="defaultValue">Default value.</param>
    /// <param name="description">Setting description.</param>
    /// <returns>The registered setting.</returns>
    public Setting<bool> RegisterBool(string key, bool defaultValue = false, string description = "")
    {
        var setting = new Setting<bool>(key, defaultValue, description);
        _settings[key] = setting;
        
        var value = GetEffectiveValue(key, defaultValue);
        setting.SetValueSilently(value);
        
        setting.ValueChanged += (sender, e) => SaveSetting(key, e.NewValue);
        
        return setting;
    }

    /// <summary>
    ///     Registers a long setting.
    /// </summary>
    /// <param name="key">Setting key name.</param>
    /// <param name="defaultValue">Default value.</param>
    /// <param name="description">Setting description.</param>
    /// <returns>The registered setting.</returns>
    public Setting<long> RegisterLong(string key, long defaultValue = 0L, string description = "")
    {
        var setting = new Setting<long>(key, defaultValue, description);
        _settings[key] = setting;
        
        var value = GetEffectiveValue(key, defaultValue);
        setting.SetValueSilently(value);
        
        setting.ValueChanged += (sender, e) => SaveSetting(key, e.NewValue);
        
        return setting;
    }

    /// <summary>
    ///     Gets a setting by key.
    /// </summary>
    /// <typeparam name="T">The setting value type.</typeparam>
    /// <param name="key">Setting key name.</param>
    /// <returns>The setting, or null if not found.</returns>
    public Setting<T> Get<T>(string key)
    {
        if (_settings.TryGetValue(key, out var setting) && setting is Setting<T> typedSetting)
        {
            return typedSetting;
        }

        return null;
    }

    /// <summary>
    ///     Checks if a setting exists and is overridden (not using default value).
    /// </summary>
    /// <param name="key">Setting key name.</param>
    /// <returns>True if the setting exists and is overridden.</returns>
    public bool IsOverridden(string key)
    {
        // First check if we have a local override
        if (_iniFile.KeyExists(_section, key))
            return true;

        // If not, check if setting object is marked as overridden
        if (_settings.TryGetValue(key, out var setting))
        {
            if (setting is Setting<string> strSetting && strSetting.IsOverridden)
                return true;
            if (setting is Setting<int> intSetting && intSetting.IsOverridden)
                return true;
            if (setting is Setting<bool> boolSetting && boolSetting.IsOverridden)
                return true;
            if (setting is Setting<long> longSetting && longSetting.IsOverridden)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Resets a setting to its default value and removes the override.
    /// </summary>
    /// <param name="key">Setting key name.</param>
    public void Reset(string key)
    {
        // Delete from INI file
        _iniFile.DeleteKey(_section, key);
        _iniFile.Save();

        // Reset setting object if it exists
        if (_settings.TryGetValue(key, out var setting))
        {
            if (setting is Setting<string> strSetting)
                strSetting.Reset();
            else if (setting is Setting<int> intSetting)
                intSetting.Reset();
            else if (setting is Setting<bool> boolSetting)
                boolSetting.Reset();
            else if (setting is Setting<long> longSetting)
                longSetting.Reset();
        }

        _logger.Debug("Reset setting: {Key}", key);
    }

    /// <summary>
    ///     Saves all settings to the INI file.
    /// </summary>
    public void SaveAll()
    {
        foreach (var kvp in _settings)
        {
            var key = kvp.Key;
            var setting = kvp.Value;

            if (setting is Setting<string> strSetting && strSetting.IsOverridden)
                _iniFile.Write(_section, key, strSetting.Value);
            else if (setting is Setting<int> intSetting && intSetting.IsOverridden)
                _iniFile.Write(_section, key, intSetting.Value);
            else if (setting is Setting<bool> boolSetting && boolSetting.IsOverridden)
                _iniFile.Write(_section, key, boolSetting.Value);
            else if (setting is Setting<long> longSetting && longSetting.IsOverridden)
                _iniFile.Write(_section, key, longSetting.Value);
        }

        _iniFile.Save();
        _logger.Information("Saved all settings to {ConfigPath}", _configFilePath);
    }

    /// <summary>
    ///     Reloads all settings from the INI file.
    /// </summary>
    public void Reload()
    {
        _iniFile.Load();

        foreach (var kvp in _settings)
        {
            var key = kvp.Key;
            var setting = kvp.Value;

            if (setting is Setting<string> strSetting)
            {
                var value = GetEffectiveValue(key, strSetting.DefaultValue);
                strSetting.SetValueSilently(value);
            }
            else if (setting is Setting<int> intSetting)
            {
                var value = GetEffectiveValue(key, intSetting.DefaultValue);
                intSetting.SetValueSilently(value);
            }
            else if (setting is Setting<bool> boolSetting)
            {
                var value = GetEffectiveValue(key, boolSetting.DefaultValue);
                boolSetting.SetValueSilently(value);
            }
            else if (setting is Setting<long> longSetting)
            {
                var value = GetEffectiveValue(key, longSetting.DefaultValue);
                longSetting.SetValueSilently(value);
            }
        }

        _logger.Information("Reloaded settings from {ConfigPath}", _configFilePath);
    }

    // Helper methods

    private T GetEffectiveValue<T>(string key, T defaultValue)
    {
        // First, check if we have a local value in the INI file
        if (_iniFile.KeyExists(_section, key))
        {
            return ReadFromIni<T>(key, defaultValue);
        }

        // If not, check parent settings (hierarchical override)
        if (_parent != null)
        {
            var parentSetting = _parent.Get<T>(key);
            if (parentSetting != null)
            {
                return parentSetting.Value;
            }
        }

        // Fall back to default
        return defaultValue;
    }

    private T ReadFromIni<T>(string key, T defaultValue)
    {
        if (typeof(T) == typeof(string))
            return (T)(object)_iniFile.Read(_section, key, defaultValue?.ToString() ?? "");
        if (typeof(T) == typeof(int))
            return (T)(object)_iniFile.ReadInt(_section, key, Convert.ToInt32(defaultValue));
        if (typeof(T) == typeof(bool))
            return (T)(object)_iniFile.ReadBool(_section, key, Convert.ToBoolean(defaultValue));
        if (typeof(T) == typeof(long))
            return (T)(object)_iniFile.ReadLong(_section, key, Convert.ToInt64(defaultValue));

        return defaultValue;
    }

    private void SaveSetting<T>(string key, T value)
    {
        if (typeof(T) == typeof(string))
            _iniFile.Write(_section, key, value?.ToString() ?? "");
        else if (typeof(T) == typeof(int))
            _iniFile.Write(_section, key, Convert.ToInt32(value));
        else if (typeof(T) == typeof(bool))
            _iniFile.Write(_section, key, Convert.ToBoolean(value));
        else if (typeof(T) == typeof(long))
            _iniFile.Write(_section, key, Convert.ToInt64(value));

        _iniFile.Save();
        _logger.Debug("Saved setting: {Key} = {Value}", key, value);
    }
}
