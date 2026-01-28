// Utils/StringTemplate.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace ObsidianLauncher.Utils;

/// <summary>
///     Simple string template system for placeholder replacement.
///     Supports ${variable}, {variable}, and %variable% syntaxes.
/// </summary>
public class StringTemplate
{
    private static readonly ILogger _logger = Log.ForContext(typeof(StringTemplate));
    
    private readonly string _template;
    private readonly Dictionary<string, string> _variables;

    /// <summary>
    ///     Creates a new string template.
    /// </summary>
    /// <param name="template">The template string with placeholders.</param>
    public StringTemplate(string template)
    {
        _template = template ?? throw new ArgumentNullException(nameof(template));
        _variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Sets a variable value.
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <param name="value">Variable value.</param>
    /// <returns>This instance for chaining.</returns>
    public StringTemplate Set(string name, string value)
    {
        _variables[name] = value ?? string.Empty;
        return this;
    }

    /// <summary>
    ///     Sets a variable value from an object (calls ToString).
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <param name="value">Variable value.</param>
    /// <returns>This instance for chaining.</returns>
    public StringTemplate Set(string name, object? value)
    {
        _variables[name] = value?.ToString() ?? string.Empty;
        return this;
    }

    /// <summary>
    ///     Sets multiple variables from a dictionary.
    /// </summary>
    /// <param name="variables">Dictionary of variable name-value pairs.</param>
    /// <returns>This instance for chaining.</returns>
    public StringTemplate SetAll(Dictionary<string, string> variables)
    {
        foreach (var kvp in variables)
        {
            _variables[kvp.Key] = kvp.Value;
        }
        return this;
    }

    /// <summary>
    ///     Renders the template with current variables.
    /// </summary>
    /// <param name="throwOnMissing">Whether to throw an exception if a variable is missing.</param>
    /// <returns>The rendered string.</returns>
    public string Render(bool throwOnMissing = false)
    {
        var result = _template;

        // Replace ${variable} style
        result = Regex.Replace(result, @"\$\{([^}]+)\}", match =>
        {
            var varName = match.Groups[1].Value;
            if (_variables.TryGetValue(varName, out var value))
            {
                return value;
            }

            if (throwOnMissing)
            {
                throw new KeyNotFoundException($"Variable not found: {varName}");
            }

            _logger.Warning("Variable not found in template: {Variable}", varName);
            return match.Value; // Keep original placeholder
        });

        // Replace {variable} style
        result = Regex.Replace(result, @"\{([^}]+)\}", match =>
        {
            var varName = match.Groups[1].Value;
            if (_variables.TryGetValue(varName, out var value))
            {
                return value;
            }

            if (throwOnMissing)
            {
                throw new KeyNotFoundException($"Variable not found: {varName}");
            }

            _logger.Warning("Variable not found in template: {Variable}", varName);
            return match.Value; // Keep original placeholder
        });

        // Replace %variable% style (Windows-like)
        result = Regex.Replace(result, @"%([^%]+)%", match =>
        {
            var varName = match.Groups[1].Value;
            if (_variables.TryGetValue(varName, out var value))
            {
                return value;
            }

            if (throwOnMissing)
            {
                throw new KeyNotFoundException($"Variable not found: {varName}");
            }

            _logger.Warning("Variable not found in template: {Variable}", varName);
            return match.Value; // Keep original placeholder
        });

        _logger.Verbose("Rendered template: {Template} -> {Result}", _template, result);
        return result;
    }

    /// <summary>
    ///     Gets all variable names found in the template.
    /// </summary>
    /// <returns>List of variable names.</returns>
    public List<string> GetVariableNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Find ${variable} style
        var matches1 = Regex.Matches(_template, @"\$\{([^}]+)\}");
        foreach (Match match in matches1)
        {
            names.Add(match.Groups[1].Value);
        }

        // Find {variable} style
        var matches2 = Regex.Matches(_template, @"\{([^}]+)\}");
        foreach (Match match in matches2)
        {
            names.Add(match.Groups[1].Value);
        }

        // Find %variable% style
        var matches3 = Regex.Matches(_template, @"%([^%]+)%");
        foreach (Match match in matches3)
        {
            names.Add(match.Groups[1].Value);
        }

        return new List<string>(names);
    }

    /// <summary>
    ///     Checks if all variables in the template have been set.
    /// </summary>
    /// <returns>True if all variables are set.</returns>
    public bool HasAllVariables()
    {
        var names = GetVariableNames();
        return names.All(name => _variables.ContainsKey(name));
    }

    /// <summary>
    ///     Gets a list of variables that haven't been set.
    /// </summary>
    /// <returns>List of missing variable names.</returns>
    public List<string> GetMissingVariables()
    {
        var names = GetVariableNames();
        return names.Where(name => !_variables.ContainsKey(name)).ToList();
    }

    /// <summary>
    ///     Static helper to quickly render a template.
    /// </summary>
    /// <param name="template">The template string.</param>
    /// <param name="variables">Dictionary of variables.</param>
    /// <param name="throwOnMissing">Whether to throw if variables are missing.</param>
    /// <returns>The rendered string.</returns>
    public static string Render(string template, Dictionary<string, string> variables, bool throwOnMissing = false)
    {
        var t = new StringTemplate(template);
        t.SetAll(variables);
        return t.Render(throwOnMissing);
    }

    /// <summary>
    ///     Static helper to quickly render a template with environment variables.
    /// </summary>
    /// <param name="template">The template string.</param>
    /// <returns>The rendered string with environment variables.</returns>
    public static string RenderWithEnvironment(string template)
    {
        var variables = EnvironmentUtils.GetAllVariables();
        return Render(template, variables, throwOnMissing: false);
    }
}
