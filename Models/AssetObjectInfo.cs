using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

/// <summary>
///     Represents information about a single asset object (e.g., texture, sound file).
///     This corresponds to the value part of the "objects" map in an asset index JSON file.
/// </summary>
public class AssetObjectInfo
{
    /// <summary>
    ///     The SHA1 hash of the asset file. This hash is also used as part of its storage path.
    /// </summary>
    [JsonPropertyName("hash")]
    public required string Hash { get; set; }

    /// <summary>
    ///     The size of the asset file in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public ulong Size { get; set; } // Using ulong for consistency with AssetIndex.Size
}