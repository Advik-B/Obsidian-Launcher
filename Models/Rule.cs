// Models/Rule.cs

using System.Collections.Generic;
using System.Text.Json.Serialization;
using ObsidianLauncher.Enums;

// Assuming RuleAction enum is here

namespace ObsidianLauncher.Models;

/// <summary>
///     Represents a rule that determines whether a feature or library is allowed or disallowed
///     based on the operating system and/or specific game features.
/// </summary>
public class Rule
{
    public Rule()
    {
        // Initialize Features dictionary to avoid null reference issues if it's accessed
        // when not present in the JSON. This is optional if you always check for null.
        // However, if the JSON can contain an empty "features": {} object,
        // System.Text.Json will create an empty dictionary anyway.
        // If "features" key is entirely missing, this property will be null without initialization.
        // For consistency with how Lists are often handled (initialized to empty),
        // initializing Dictionary can also be a good practice.
        Features = new Dictionary<string, bool>();
    }

    /// <summary>
    ///     The action to take if the conditions of this rule are met ("allow" or "disallow").
    /// </summary>
    [JsonPropertyName("action")]
    [JsonConverter(typeof(JsonStringEnumConverter))] // Handles "allow" / "disallow"
    public RuleAction Action { get; set; }

    /// <summary>
    ///     Operating system specific conditions for this rule.
    ///     This property will be null if the rule does not depend on the OS.
    /// </summary>
    [JsonPropertyName("os")]
    public OperatingSystemInfo Os { get; set; } // Nullable if "os" object might be absent

    /// <summary>
    ///     Feature-specific conditions for this rule.
    ///     The key is the feature name (e.g., "is_demo_user", "has_custom_resolution")
    ///     and the value is a boolean indicating if that feature must be true for this rule's OS/Action part to be considered.
    ///     This property will be null if the rule does not depend on specific game features.
    /// </summary>
    [JsonPropertyName("features")]
    public Dictionary<string, bool> Features { get; set; } // Nullable if "features" object might be absent
}