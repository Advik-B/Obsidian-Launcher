using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models
{
    /// <summary>
    /// Represents a library dependency for a Minecraft version.
    /// </summary>
    public class Library
    {
        /// <summary>
        /// The Maven-style name of the library (e.g., "com.mojang:brigadier:1.0.18").
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Download information for the library's artifacts. Can be null if not specified.
        /// </summary>
        [JsonPropertyName("downloads")]
        public LibraryDownloads Downloads { get; set; } // Nullable if "downloads" object might be absent

        /// <summary>
        /// A list of rules that determine if this library should be included based on OS or features.
        /// </summary>
        [JsonPropertyName("rules")]
        public List<Rule> Rules { get; set; } // Can be null or empty

        /// <summary>
        /// A mapping from OS name (e.g., "windows", "linux", "osx") to the classifier key
        /// for the native library for that OS. Example: {"linux": "natives-linux"}.
        /// This is used to select the correct native JAR to download and extract.
        /// </summary>
        [JsonPropertyName("natives")]
        public Dictionary<string, string> Natives { get; set; } // Can be null or empty

        /// <summary>
        /// Extraction rules, typically for native libraries, specifying paths to exclude.
        /// Can be null if no special extraction rules apply.
        /// </summary>
        [JsonPropertyName("extract")]
        public LibraryExtractRule Extract { get; set; } // Nullable if "extract" object might be absent

        public Library()
        {
            // Initialize collections to prevent null reference exceptions
            Rules = new List<Rule>();
            Natives = new Dictionary<string, string>();
        }
    }
}