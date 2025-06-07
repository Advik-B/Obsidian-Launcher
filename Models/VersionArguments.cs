// Models/VersionArguments.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models
{
    /// <summary>
    /// Contains the game and JVM arguments for a Minecraft version.
    /// Corresponds to the "arguments" object in the Minecraft version manifest.
    /// </summary>
    public class VersionArguments
    {
        /// <summary>
        /// List of game arguments. Each element can be a plain string or a conditional argument.
        /// </summary>
        [JsonPropertyName("game")]
        public List<VersionArgument> Game { get; set; }

        /// <summary>
        /// List of JVM arguments. Each element can be a plain string or a conditional argument.
        /// </summary>
        [JsonPropertyName("jvm")]
        public List<VersionArgument> Jvm { get; set; }

        public VersionArguments()
        {
            Game = new List<VersionArgument>();
            Jvm = new List<VersionArgument>();
        }
    }
}