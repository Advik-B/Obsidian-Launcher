// Models/ArgumentRuleCondition.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ObsidianLauncher.Enums;

namespace ObsidianLauncher.Models
{
    /// <summary>
    /// Represents a condition for applying a version argument, based on OS or features.
    /// </summary>
    public class ArgumentRuleCondition
    {
        [JsonPropertyName("action")]
        [JsonConverter(typeof(JsonStringEnumConverter))] // For "allow" / "disallow" strings
        public RuleAction Action { get; set; }

        [JsonPropertyName("os")]
        public OperatingSystemInfo Os { get; set; } // Nullable if "os" object might be absent

        [JsonPropertyName("features")]
        public Dictionary<string, bool> Features { get; set; } // Nullable if "features" object might be absent

        public ArgumentRuleCondition()
        {
            Features = new Dictionary<string, bool>(); // Initialize to avoid null if not present
        }
    }
}