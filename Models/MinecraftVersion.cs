using System.Collections.Generic;
using System.Text.Json.Serialization; // For JsonPropertyName
// Import other necessary model namespaces if they are separate
// using ObsidianLauncher.Models.Arguments; // If Arguments is in a sub-namespace or different file
// using ObsidianLauncher.Models.Logging;   // If LoggingInfo is in a sub-namespace or different file

namespace ObsidianLauncher.Models
{
    /// <summary>
    /// Represents the detailed information for a specific Minecraft version,
    /// parsed from the version-specific JSON file.
    /// </summary>
    public class MinecraftVersion
    {
        [JsonPropertyName("assetIndex")]
        public AssetIndex AssetIndex { get; set; }

        [JsonPropertyName("assets")]
        public string Assets { get; set; } // e.g., "1.20" or "legacy"

        [JsonPropertyName("complianceLevel")]
        public int? ComplianceLevel { get; set; } // Optional, hence nullable int

        [JsonPropertyName("downloads")]
        public Dictionary<string, DownloadDetails> Downloads { get; set; } // Key: "client", "server", "client_mappings", etc.

        [JsonPropertyName("id")]
        public string Id { get; set; } // e.g., "1.20.4"

        [JsonPropertyName("javaVersion")]
        public JavaVersionInfo JavaVersion { get; set; } // Optional, so could be nullable if not always present

        [JsonPropertyName("libraries")]
        public List<Library> Libraries { get; set; }

        [JsonPropertyName("mainClass")]
        public string MainClass { get; set; } // Optional, e.g., "net.minecraft.client.main.Main"

        [JsonPropertyName("minecraftArguments")]
        public string MinecraftArguments { get; set; } // For older versions, optional

        [JsonPropertyName("minimumLauncherVersion")]
        public int? MinimumLauncherVersion { get; set; } // Optional

        [JsonPropertyName("releaseTime")]
        public System.DateTime ReleaseTime { get; set; } // Can be parsed from ISO 8601 string

        [JsonPropertyName("time")]
        public System.DateTime Time { get; set; } // Can be parsed from ISO 8601 string

        [JsonPropertyName("type")]
        public string Type { get; set; } // e.g., "release", "snapshot", "old_alpha"

        // Newer version manifest fields
        [JsonPropertyName("arguments")]
        public VersionArguments Arguments { get; set; } // Optional

        [JsonPropertyName("logging")]
        public VersionLogging Logging { get; set; } // Optional

        public MinecraftVersion()
        {
            // Initialize collections to avoid null reference exceptions if they are not in the JSON
            Downloads = new Dictionary<string, DownloadDetails>();
            Libraries = new List<Library>();
        }
    }

    // Note: The C++ code had MinecraftJARType enum for the keys of the downloads map.
    // In C#, for System.Text.Json deserialization into a Dictionary<string, TValue>,
    // the keys from JSON will directly be used as strings.
    // If you need to map these string keys to an enum *after* deserialization,
    // you'd do that in your processing logic.
    // Alternatively, you could write a custom JsonConverter for the Dictionary
    // if you absolutely need the keys to be enums directly after deserialization,
    // but that adds complexity. Using string keys is simpler here.
}