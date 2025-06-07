// Models/MinecraftVersion.cs
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models
{
    public class MinecraftVersion
    {
        [JsonPropertyName("assetIndex")]
        public AssetIndex AssetIndex { get; set; } // Can be null for very early versions if manifest structure differs

        [JsonPropertyName("assets")]
        public string Assets { get; set; } // e.g., "24", "pre-1.6"

        [JsonPropertyName("complianceLevel")]
        public int? ComplianceLevel { get; set; }

        [JsonPropertyName("downloads")]
        public Dictionary<string, DownloadDetails> Downloads { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("javaVersion")]
        public JavaVersionInfo JavaVersion { get; set; } // Can be null

        [JsonPropertyName("libraries")]
        public List<Library> Libraries { get; set; }

        [JsonPropertyName("mainClass")]
        public string MainClass { get; set; }

        [JsonPropertyName("minecraftArguments")]
        public string MinecraftArguments { get; set; } // Present in older versions, null in newer

        [JsonPropertyName("minimumLauncherVersion")]
        public int? MinimumLauncherVersion { get; set; }

        [JsonPropertyName("releaseTime")]
        public DateTime ReleaseTime { get; set; }

        [JsonPropertyName("time")]
        public DateTime Time { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        // Newer version manifest fields - can be null for older versions
        [JsonPropertyName("arguments")]
        public VersionArguments Arguments { get; set; }

        [JsonPropertyName("logging")]
        public VersionLogging Logging { get; set; }

        public MinecraftVersion()
        {
            Downloads = new Dictionary<string, DownloadDetails>();
            Libraries = new List<Library>();
            // Optional objects like AssetIndex, JavaVersion, Arguments, Logging
            // will be null if not present in JSON, which is fine.
        }
    }
}