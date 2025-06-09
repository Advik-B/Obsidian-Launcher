// Models/VersionManifest.cs

using System.Collections.Generic;
using System.Text.Json.Serialization;

// For JsonPropertyName

namespace ObsidianLauncher.Models;

/// <summary>
///     Represents the top-level structure of Mojang's Minecraft version manifest (version_manifest_v2.json).
///     It contains information about the latest game versions and a list of all available versions.
/// </summary>
public class VersionManifest
{
    public VersionManifest()
    {
        // Initialize collections to avoid null issues if JSON is sparse or during manual creation
        Versions = new List<VersionMetadata>();
    }

    /// <summary>
    ///     Information about the latest release and snapshot versions.
    /// </summary>
    [JsonPropertyName("latest")]
    public LatestVersionInfo Latest { get; set; }

    /// <summary>
    ///     A list of metadata for all available Minecraft versions.
    /// </summary>
    [JsonPropertyName("versions")]
    public List<VersionMetadata> Versions { get; set; }
}

/// <summary>
///     Contains identifiers for the latest release and snapshot versions of Minecraft.
///     Corresponds to the "latest" object within the main version manifest.
/// </summary>
public class LatestVersionInfo
{
    /// <summary>
    ///     The ID of the latest official release version (e.g., "1.20.4").
    /// </summary>
    [JsonPropertyName("release")]
    public string Release { get; set; }

    /// <summary>
    ///     The ID of the latest snapshot version (e.g., "23w45a").
    /// </summary>
    [JsonPropertyName("snapshot")]
    public string Snapshot { get; set; }
}