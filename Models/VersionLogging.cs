using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

/// <summary>
///     Contains logging configurations for a Minecraft version, typically for the client.
///     Corresponds to the "logging" object in the Minecraft version manifest.
/// </summary>
public class VersionLogging // Renamed from LoggingInfo to be more descriptive at the top level
{
    /// <summary>
    ///     Client-specific logging configuration. This property might be null if no client logging is specified.
    /// </summary>
    [JsonPropertyName("client")]
    public ClientLoggingInfo? Client { get; set; } // Nullable if "client" object might be absent
}