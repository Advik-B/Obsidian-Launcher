// Models/JavaVersionInfo.cs
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models
{
    /// <summary>
    /// Represents the Java version requirements for a Minecraft version.
    /// Corresponds to the "javaVersion" object in the Minecraft version manifest.
    /// </summary>
    public class JavaVersionInfo
    {
        /// <summary>
        /// The component name of the Java runtime, e.g., "jre-legacy", "minecraft-java-runtime-alpha".
        /// </summary>
        [JsonPropertyName("component")]
        public required string  Component { get; set; }

        /// <summary>
        /// The major version number of the Java runtime, e.g., 8, 16, 17.
        /// </summary>
        [JsonPropertyName("majorVersion")]
        public uint MajorVersion { get; set; } // Using uint as it's unsigned in C++ and logically non-negative
    }
}