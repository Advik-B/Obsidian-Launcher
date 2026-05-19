// Utils/TomlFile.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Serialization;

namespace ObsidianLauncher.Utils;

/// <summary>
///     TOML file parser and writer for configuration management.
///     Supports sections (tables), key-value pairs, and preserves TOML structure.
/// </summary>
public class TomlFile
{
    private static readonly ILogger _logger = Log.ForContext(typeof(TomlFile));
    private readonly TomlTable _root;
    private readonly string _filePath;

    /// <summary>
    ///     Creates a new TOML file handler.
    /// </summary>
    /// <param name="filePath">Path to the TOML file.</param>
    public TomlFile(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _root = new TomlTable();

        if (File.Exists(filePath))
        {
            Load();
        }
    }

    /// <summary>
    ///     Loads the TOML file from disk.
    /// </summary>
    public void Load()
    {
        try
        {
            _root.Clear();

            if (!File.Exists(_filePath))
            {
                _logger.Information("TOML file does not exist, starting with empty configuration: {FilePath}", _filePath);
                return;
            }

            var tomlContent = File.ReadAllText(_filePath);
            var loadedTable = TomlSerializer.Deserialize<TomlTable>(tomlContent, TomlSerializerOptions.Default);

            // Copy all items from loaded table to our root
            if (loadedTable != null)
            {
                foreach (var kvp in loadedTable)
                {
                    _root[kvp.Key] = kvp.Value;
                }
            }

            _logger.Information("Loaded TOML file: {FilePath} with {SectionCount} top-level entries",
                _filePath, _root.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load TOML file: {FilePath}", _filePath);
        }
    }

    /// <summary>
    ///     Saves the TOML file to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var tomlString = TomlSerializer.Serialize<TomlTable>(_root, TomlSerializerOptions.Default);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, tomlString);
            _logger.Information("Saved TOML file: {FilePath}", _filePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save TOML file: {FilePath}", _filePath);
        }
    }

    /// <summary>
    ///     Reads a string value from the TOML file.
    /// </summary>
    /// <param name="section">Section name (use empty string for root).</param>
    /// <param name="key">Key name.</param>
    /// <param name="defaultValue">Default value if key doesn't exist.</param>
    /// <returns>The value or default value.</returns>
    public string Read(string section, string key, string defaultValue = "")
    {
        section ??= string.Empty;

        try
        {
            if (string.IsNullOrEmpty(section))
            {
                // Read from root
                if (_root.TryGetValue(key, out var value) && value is string strValue)
                {
                    return strValue;
                }
            }
            else
            {
                // Read from section
                if (_root.TryGetValue(section, out var sectionObj) && sectionObj is TomlTable sectionTable)
                {
                    if (sectionTable.TryGetValue(key, out var value) && value is string strValue)
                    {
                        return strValue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error reading TOML key: [{Section}].{Key}", section, key);
        }

        return defaultValue;
    }

    /// <summary>
    ///     Reads an integer value from the TOML file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <param name="key">Key name.</param>
    /// <param name="defaultValue">Default value if key doesn't exist or can't be parsed.</param>
    /// <returns>The integer value or default value.</returns>
    public int ReadInt(string section, string key, int defaultValue = 0)
    {
        section ??= string.Empty;

        try
        {
            object? value = null;

            if (string.IsNullOrEmpty(section))
            {
                _root.TryGetValue(key, out value);
            }
            else
            {
                if (_root.TryGetValue(section, out var sectionObj) && sectionObj is TomlTable sectionTable)
                {
                    sectionTable.TryGetValue(key, out value);
                }
            }

            if (value != null)
            {
                if (value is long longValue)
                    return (int)longValue;
                if (value is int intValue)
                    return intValue;
                if (value is string strValue && int.TryParse(strValue, out var parsedInt))
                    return parsedInt;
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error reading TOML integer key: [{Section}].{Key}", section, key);
        }

        return defaultValue;
    }

    /// <summary>
    ///     Reads a boolean value from the TOML file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <param name="key">Key name.</param>
    /// <param name="defaultValue">Default value if key doesn't exist or can't be parsed.</param>
    /// <returns>The boolean value or default value.</returns>
    public bool ReadBool(string section, string key, bool defaultValue = false)
    {
        section ??= string.Empty;

        try
        {
            object? value = null;

            if (string.IsNullOrEmpty(section))
            {
                _root.TryGetValue(key, out value);
            }
            else
            {
                if (_root.TryGetValue(section, out var sectionObj) && sectionObj is TomlTable sectionTable)
                {
                    sectionTable.TryGetValue(key, out value);
                }
            }

            if (value != null)
            {
                if (value is bool boolValue)
                    return boolValue;
                if (value is string strValue && bool.TryParse(strValue, out var parsedBool))
                    return parsedBool;
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error reading TOML boolean key: [{Section}].{Key}", section, key);
        }

        return defaultValue;
    }

    /// <summary>
    ///     Reads a long value from the TOML file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <param name="key">Key name.</param>
    /// <param name="defaultValue">Default value if key doesn't exist or can't be parsed.</param>
    /// <returns>The long value or default value.</returns>
    public long ReadLong(string section, string key, long defaultValue = 0)
    {
        section ??= string.Empty;

        try
        {
            object? value = null;

            if (string.IsNullOrEmpty(section))
            {
                _root.TryGetValue(key, out value);
            }
            else
            {
                if (_root.TryGetValue(section, out var sectionObj) && sectionObj is TomlTable sectionTable)
                {
                    sectionTable.TryGetValue(key, out value);
                }
            }

            if (value != null)
            {
                if (value is long longValue)
                    return longValue;
                if (value is int intValue)
                    return intValue;
                if (value is string strValue && long.TryParse(strValue, out var parsedLong))
                    return parsedLong;
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error reading TOML long key: [{Section}].{Key}", section, key);
        }

        return defaultValue;
    }

    /// <summary>
    ///     Writes a string value to the TOML file.
    /// </summary>
    /// <param name="section">Section name (use empty string for root).</param>
    /// <param name="key">Key name.</param>
    /// <param name="value">Value to write.</param>
    public void Write(string section, string key, string value)
    {
        section ??= string.Empty;

        try
        {
            if (string.IsNullOrEmpty(section))
            {
                // Write to root
                _root[key] = value ?? string.Empty;
            }
            else
            {
                // Write to section
                if (!_root.TryGetValue(section, out var sectionObj) || sectionObj is not TomlTable)
                {
                    _root[section] = new TomlTable();
                }

                var sectionTable = (TomlTable)_root[section];
                sectionTable[key] = value ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing TOML key: [{Section}].{Key}", section, key);
        }
    }

    /// <summary>
    ///     Writes an integer value to the TOML file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <param name="key">Key name.</param>
    /// <param name="value">Value to write.</param>
    public void Write(string section, string key, int value)
    {
        section ??= string.Empty;

        try
        {
            if (string.IsNullOrEmpty(section))
            {
                _root[key] = (long)value;
            }
            else
            {
                if (!_root.TryGetValue(section, out var sectionObj) || sectionObj is not TomlTable)
                {
                    _root[section] = new TomlTable();
                }

                var sectionTable = (TomlTable)_root[section];
                sectionTable[key] = (long)value;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing TOML integer key: [{Section}].{Key}", section, key);
        }
    }

    /// <summary>
    ///     Writes a boolean value to the TOML file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <param name="key">Key name.</param>
    /// <param name="value">Value to write.</param>
    public void Write(string section, string key, bool value)
    {
        section ??= string.Empty;

        try
        {
            if (string.IsNullOrEmpty(section))
            {
                _root[key] = value;
            }
            else
            {
                if (!_root.TryGetValue(section, out var sectionObj) || sectionObj is not TomlTable)
                {
                    _root[section] = new TomlTable();
                }

                var sectionTable = (TomlTable)_root[section];
                sectionTable[key] = value;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing TOML boolean key: [{Section}].{Key}", section, key);
        }
    }

    /// <summary>
    ///     Writes a long value to the TOML file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <param name="key">Key name.</param>
    /// <param name="value">Value to write.</param>
    public void Write(string section, string key, long value)
    {
        section ??= string.Empty;

        try
        {
            if (string.IsNullOrEmpty(section))
            {
                _root[key] = value;
            }
            else
            {
                if (!_root.TryGetValue(section, out var sectionObj) || sectionObj is not TomlTable)
                {
                    _root[section] = new TomlTable();
                }

                var sectionTable = (TomlTable)_root[section];
                sectionTable[key] = value;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing TOML long key: [{Section}].{Key}", section, key);
        }
    }

    /// <summary>
    ///     Deletes a key from the TOML file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <param name="key">Key name.</param>
    /// <returns>True if the key was deleted, false if it didn't exist.</returns>
    public bool DeleteKey(string section, string key)
    {
        section ??= string.Empty;

        try
        {
            if (string.IsNullOrEmpty(section))
            {
                return _root.Remove(key);
            }
            else
            {
                if (_root.TryGetValue(section, out var sectionObj) && sectionObj is TomlTable sectionTable)
                {
                    return sectionTable.Remove(key);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error deleting TOML key: [{Section}].{Key}", section, key);
        }

        return false;
    }

    /// <summary>
    ///     Deletes an entire section from the TOML file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <returns>True if the section was deleted, false if it didn't exist.</returns>
    public bool DeleteSection(string section)
    {
        section ??= string.Empty;

        if (string.IsNullOrEmpty(section))
        {
            _logger.Warning("Cannot delete root section");
            return false;
        }

        return _root.Remove(section);
    }

    /// <summary>
    ///     Checks if a section exists.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <returns>True if the section exists.</returns>
    public bool SectionExists(string section)
    {
        section ??= string.Empty;

        if (string.IsNullOrEmpty(section))
            return true; // Root always exists

        return _root.ContainsKey(section) && _root[section] is TomlTable;
    }

    /// <summary>
    ///     Checks if a key exists in a section.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <param name="key">Key name.</param>
    /// <returns>True if the key exists.</returns>
    public bool KeyExists(string section, string key)
    {
        section ??= string.Empty;

        if (string.IsNullOrEmpty(section))
        {
            return _root.ContainsKey(key);
        }
        else
        {
            return _root.TryGetValue(section, out var sectionObj) &&
                   sectionObj is TomlTable sectionTable &&
                   sectionTable.ContainsKey(key);
        }
    }

    /// <summary>
    ///     Gets all section names.
    /// </summary>
    /// <returns>Array of section names.</returns>
    public string[] GetSections()
    {
        return _root.Where(kvp => kvp.Value is TomlTable)
                   .Select(kvp => kvp.Key)
                   .ToArray();
    }

    /// <summary>
    ///     Gets all keys in a section.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <returns>Array of key names, or empty array if section doesn't exist.</returns>
    public string[] GetKeys(string section)
    {
        section ??= string.Empty;

        if (string.IsNullOrEmpty(section))
        {
            return _root.Keys.ToArray();
        }
        else
        {
            if (_root.TryGetValue(section, out var sectionObj) && sectionObj is TomlTable sectionTable)
            {
                return sectionTable.Keys.ToArray();
            }
        }

        return Array.Empty<string>();
    }
}
