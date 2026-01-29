// Utils/EnvironmentUtils.cs

using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace ObsidianLauncher.Utils;

/// <summary>
///     Utility class for environment variable management and helpers.
/// </summary>
public static class EnvironmentUtils
{
    private static readonly ILogger _logger = Log.ForContext(typeof(EnvironmentUtils));

    /// <summary>
    ///     Gets an environment variable value.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="defaultValue">Default value if variable doesn't exist.</param>
    /// <returns>The variable value or default.</returns>
    public static string GetVariable(string name, string defaultValue = "")
    {
        var value = Environment.GetEnvironmentVariable(name);
        return value ?? defaultValue;
    }

    /// <summary>
    ///     Gets an environment variable as an integer.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="defaultValue">Default value if variable doesn't exist or can't be parsed.</param>
    /// <returns>The variable value as integer or default.</returns>
    public static int GetVariableAsInt(string name, int defaultValue = 0)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    ///     Gets an environment variable as a boolean.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="defaultValue">Default value if variable doesn't exist or can't be parsed.</param>
    /// <returns>The variable value as boolean or default.</returns>
    public static bool GetVariableAsBool(string name, bool defaultValue = false)
    {
        var value = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrEmpty(value))
            return defaultValue;

        // Support common boolean representations
        value = value.ToLowerInvariant();
        if (value == "true" || value == "1" || value == "yes" || value == "on")
            return true;
        if (value == "false" || value == "0" || value == "no" || value == "off")
            return false;

        return defaultValue;
    }

    /// <summary>
    ///     Sets an environment variable for the current process.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The variable value.</param>
    /// <param name="target">The target scope (Process, User, or Machine).</param>
    public static void SetVariable(string name, string value, EnvironmentVariableTarget target = EnvironmentVariableTarget.Process)
    {
        try
        {
            Environment.SetEnvironmentVariable(name, value, target);
            _logger.Debug("Set environment variable {Name} = {Value} (target: {Target})", name, value, target);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set environment variable {Name}", name);
        }
    }

    /// <summary>
    ///     Checks if an environment variable exists.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <returns>True if the variable exists.</returns>
    public static bool VariableExists(string name)
    {
        return Environment.GetEnvironmentVariable(name) != null;
    }

    /// <summary>
    ///     Gets all environment variables as a dictionary.
    /// </summary>
    /// <returns>Dictionary of all environment variables.</returns>
    public static Dictionary<string, string> GetAllVariables()
    {
        var variables = new Dictionary<string, string>();

        foreach (var entry in Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>())
        {
            if (entry.Key != null && entry.Value != null)
            {
                variables[entry.Key.ToString()!] = entry.Value.ToString()!;
            }
        }

        return variables;
    }

    /// <summary>
    ///     Expands environment variables in a string (e.g., "%USERPROFILE%\path" or "$HOME/path").
    /// </summary>
    /// <param name="path">The path with environment variables.</param>
    /// <returns>The expanded path.</returns>
    public static string ExpandVariables(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        try
        {
            // Handle Windows-style %VAR%
            var expanded = Environment.ExpandEnvironmentVariables(path);

            // Handle Unix-style $VAR
            if (expanded.Contains('$'))
            {
                var variables = GetAllVariables();
                foreach (var kvp in variables)
                {
                    expanded = expanded.Replace($"${kvp.Key}", kvp.Value);
                    expanded = expanded.Replace($"${{{kvp.Key}}}", kvp.Value);
                }
            }

            _logger.Verbose("Expanded variables: {Original} -> {Expanded}", path, expanded);
            return expanded;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to expand environment variables in: {Path}", path);
            return path;
        }
    }

    /// <summary>
    ///     Gets the PATH environment variable as a list of directories.
    /// </summary>
    /// <returns>List of directories in PATH.</returns>
    public static List<string> GetPathDirectories()
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        var separator = System.IO.Path.PathSeparator;

        return pathVar.Split(separator, StringSplitOptions.RemoveEmptyEntries)
                      .Select(p => p.Trim())
                      .Where(p => !string.IsNullOrEmpty(p))
                      .ToList();
    }

    /// <summary>
    ///     Adds a directory to the PATH environment variable for the current process.
    /// </summary>
    /// <param name="directory">The directory to add.</param>
    public static void AddToPath(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        var separator = System.IO.Path.PathSeparator;

        // Check if already in PATH
        var paths = GetPathDirectories();
        if (paths.Contains(directory, StringComparer.OrdinalIgnoreCase))
        {
            _logger.Debug("Directory already in PATH: {Directory}", directory);
            return;
        }

        // Add to PATH
        var newPath = $"{directory}{separator}{pathVar}";
        Environment.SetEnvironmentVariable("PATH", newPath);

        _logger.Information("Added directory to PATH: {Directory}", directory);
    }

    /// <summary>
    ///     Gets common system environment variable values.
    /// </summary>
    /// <returns>Dictionary of common system variables.</returns>
    public static Dictionary<string, string> GetSystemInfo()
    {
        return new Dictionary<string, string>
        {
            ["OS"] = Environment.OSVersion.ToString(),
            ["Platform"] = Environment.OSVersion.Platform.ToString(),
            ["MachineName"] = Environment.MachineName,
            ["UserName"] = Environment.UserName,
            ["UserDomainName"] = Environment.UserDomainName,
            ["ProcessorCount"] = Environment.ProcessorCount.ToString(),
            ["Is64BitOS"] = Environment.Is64BitOperatingSystem.ToString(),
            ["Is64BitProcess"] = Environment.Is64BitProcess.ToString(),
            ["CLRVersion"] = Environment.Version.ToString(),
            ["CurrentDirectory"] = Environment.CurrentDirectory,
            ["SystemDirectory"] = Environment.SystemDirectory
        };
    }
}
