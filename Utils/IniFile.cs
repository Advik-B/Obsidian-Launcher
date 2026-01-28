// Utils/IniFile.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Serilog;

namespace ObsidianLauncher.Utils;

/// <summary>
///     Simple INI file parser and writer for configuration management.
///     Supports sections, key-value pairs, and comments.
/// </summary>
public class IniFile
{
    private static readonly ILogger _logger = Log.ForContext(typeof(IniFile));
    private readonly Dictionary<string, Dictionary<string, string>> _sections;
    private readonly string _filePath;

    /// <summary>
    ///     Creates a new INI file handler.
    /// </summary>
    /// <param name="filePath">Path to the INI file.</param>
    public IniFile(string filePath)
    {
        _filePath = filePath;
        _sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        
        if (File.Exists(filePath))
        {
            Load();
        }
    }

    /// <summary>
    ///     Loads the INI file from disk.
    /// </summary>
    public void Load()
    {
        try
        {
            _sections.Clear();
            
            if (!File.Exists(_filePath))
            {
                _logger.Information("INI file does not exist, starting with empty configuration: {FilePath}", _filePath);
                return;
            }

            var lines = File.ReadAllLines(_filePath);
            string currentSection = string.Empty;
            
            // Ensure default section exists
            if (!_sections.ContainsKey(currentSection))
            {
                _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                    continue;

                // Section header
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                    if (!_sections.ContainsKey(currentSection))
                    {
                        _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                    continue;
                }

                // Key-value pair
                var separatorIndex = trimmedLine.IndexOf('=');
                if (separatorIndex > 0)
                {
                    var key = trimmedLine.Substring(0, separatorIndex).Trim();
                    var value = trimmedLine.Substring(separatorIndex + 1).Trim();
                    
                    // Remove quotes if present
                    if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    _sections[currentSection][key] = value;
                }
            }

            _logger.Information("Loaded INI file: {FilePath} with {SectionCount} sections", _filePath, _sections.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load INI file: {FilePath}", _filePath);
        }
    }

    /// <summary>
    ///     Saves the INI file to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var sb = new StringBuilder();
            
            // Write default section first (no section header)
            if (_sections.ContainsKey(string.Empty) && _sections[string.Empty].Count > 0)
            {
                foreach (var kvp in _sections[string.Empty])
                {
                    sb.AppendLine($"{kvp.Key}={QuoteIfNeeded(kvp.Value)}");
                }
                sb.AppendLine();
            }

            // Write other sections
            foreach (var section in _sections.Where(s => !string.IsNullOrEmpty(s.Key)))
            {
                sb.AppendLine($"[{section.Key}]");
                foreach (var kvp in section.Value)
                {
                    sb.AppendLine($"{kvp.Key}={QuoteIfNeeded(kvp.Value)}");
                }
                sb.AppendLine();
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, sb.ToString());
            _logger.Information("Saved INI file: {FilePath}", _filePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save INI file: {FilePath}", _filePath);
        }
    }

    /// <summary>
    ///     Reads a string value from the INI file.
    /// </summary>
    /// <param name="section">Section name (use empty string for default section).</param>
    /// <param name="key">Key name.</param>
    /// <param name="defaultValue">Default value if key doesn't exist.</param>
    /// <returns>The value or default value.</returns>
    public string Read(string section, string key, string defaultValue = "")
    {
        section ??= string.Empty;
        
        if (_sections.TryGetValue(section, out var sectionData) && sectionData.TryGetValue(key, out var value))
        {
            return value;
        }

        return defaultValue;
    }

    /// <summary>
    ///     Reads an integer value from the INI file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <param name="key">Key name.</param>
    /// <param name="defaultValue">Default value if key doesn't exist or can't be parsed.</param>
    /// <returns>The integer value or default value.</returns>
    public int ReadInt(string section, string key, int defaultValue = 0)
    {
        var value = Read(section, key);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    ///     Reads a boolean value from the INI file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <param name="key">Key name.</param>
    /// <param name="defaultValue">Default value if key doesn't exist or can't be parsed.</param>
    /// <returns>The boolean value or default value.</returns>
    public bool ReadBool(string section, string key, bool defaultValue = false)
    {
        var value = Read(section, key);
        if (bool.TryParse(value, out var result))
            return result;
        
        // Support numeric representation (1 = true, 0 = false)
        if (int.TryParse(value, out var numValue))
            return numValue != 0;

        return defaultValue;
    }

    /// <summary>
    ///     Reads a long value from the INI file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <param name="key">Key name.</param>
    /// <param name="defaultValue">Default value if key doesn't exist or can't be parsed.</param>
    /// <returns>The long value or default value.</returns>
    public long ReadLong(string section, string key, long defaultValue = 0)
    {
        var value = Read(section, key);
        return long.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    ///     Writes a string value to the INI file.
    /// </summary>
    /// <param name="section">Section name (use empty string for default section).</param>
    /// <param name="key">Key name.</param>
    /// <param name="value">Value to write.</param>
    public void Write(string section, string key, string value)
    {
        section ??= string.Empty;
        
        if (!_sections.ContainsKey(section))
        {
            _sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        _sections[section][key] = value ?? string.Empty;
    }

    /// <summary>
    ///     Writes an integer value to the INI file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <param name="key">Key name.</param>
    /// <param name="value">Value to write.</param>
    public void Write(string section, string key, int value)
    {
        Write(section, key, value.ToString());
    }

    /// <summary>
    ///     Writes a boolean value to the INI file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <param name="key">Key name.</param>
    /// <param name="value">Value to write.</param>
    public void Write(string section, string key, bool value)
    {
        Write(section, key, value.ToString().ToLower());
    }

    /// <summary>
    ///     Writes a long value to the INI file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <param name="key">Key name.</param>
    /// <param name="value">Value to write.</param>
    public void Write(string section, string key, long value)
    {
        Write(section, key, value.ToString());
    }

    /// <summary>
    ///     Deletes a key from the INI file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <param name="key">Key name.</param>
    /// <returns>True if the key was deleted, false if it didn't exist.</returns>
    public bool DeleteKey(string section, string key)
    {
        section ??= string.Empty;
        
        if (_sections.TryGetValue(section, out var sectionData))
        {
            return sectionData.Remove(key);
        }

        return false;
    }

    /// <summary>
    ///     Deletes an entire section from the INI file.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <returns>True if the section was deleted, false if it didn't exist.</returns>
    public bool DeleteSection(string section)
    {
        section ??= string.Empty;
        return _sections.Remove(section);
    }

    /// <summary>
    ///     Checks if a section exists.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <returns>True if the section exists.</returns>
    public bool SectionExists(string section)
    {
        section ??= string.Empty;
        return _sections.ContainsKey(section);
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
        return _sections.TryGetValue(section, out var sectionData) && sectionData.ContainsKey(key);
    }

    /// <summary>
    ///     Gets all section names.
    /// </summary>
    /// <returns>Array of section names.</returns>
    public string[] GetSections()
    {
        return _sections.Keys.ToArray();
    }

    /// <summary>
    ///     Gets all keys in a section.
    /// </summary>
    /// <param name="section">Section name.</param>
    /// <returns>Array of key names, or empty array if section doesn't exist.</returns>
    public string[] GetKeys(string section)
    {
        section ??= string.Empty;
        return _sections.TryGetValue(section, out var sectionData) 
            ? sectionData.Keys.ToArray() 
            : Array.Empty<string>();
    }

    /// <summary>
    ///     Quotes a value if it contains special characters.
    /// </summary>
    private string QuoteIfNeeded(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Quote if contains spaces, equals, or special characters
        if (value.Contains(' ') || value.Contains('=') || value.Contains(';') || value.Contains('#'))
        {
            return $"\"{value}\"";
        }

        return value;
    }
}
