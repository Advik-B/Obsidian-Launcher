// Models/ConditionalArgumentValue.cs

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

// Assuming ArgumentRuleCondition is in the same namespace or imported

namespace ObsidianLauncher.Models;

/// <summary>
///     Represents an argument or set of arguments that are applied based on a list of rules.
///     The "value" can be a single string or an array of strings.
/// </summary>
public class ConditionalArgumentValue
{
    public ConditionalArgumentValue()
    {
        Rules = new List<ArgumentRuleCondition>();
    }

    [JsonPropertyName("rules")] public List<ArgumentRuleCondition> Rules { get; set; }

    /// <summary>
    ///     The argument value(s). Can be a single string or a list of strings.
    ///     System.Text.Json will deserialize a JSON string to string, and a JSON array of strings to List
    ///     <string>
    ///         .
    ///         You will need to check the type after deserialization or use a custom converter if you want it strongly typed
    ///         to object.
    ///         For simplicity, we'll often see this represented as object and then cast.
    ///         A more robust way with System.Text.Json is often to handle this in post-processing or with a JsonConverter.
    ///         Here, we'll use object and expect the consuming code to check.
    /// </summary>
    [JsonPropertyName("value")]
    public object Value { get; set; } // Can be string or List<string>

    // Helper methods to access the value in a typed way
    public string GetSingleValue()
    {
        return Value as string;
    }

    public List<string> GetListValue()
    {
        if (Value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            return element.Deserialize<List<string>>();
        return Value as List<string>; // If it was already deserialized as List<string>
    }

    public bool IsSingleValue()
    {
        return Value is string || (Value is JsonElement e && e.ValueKind == JsonValueKind.String);
    }

    public bool IsListValue()
    {
        return Value is List<string> || (Value is JsonElement e && e.ValueKind == JsonValueKind.Array);
    }
}