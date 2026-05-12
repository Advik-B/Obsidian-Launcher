// Models/AssetIndex.cs

using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

/// <summary>
///     Represents metadata about the asset index for a Minecraft version.
///     This object points to another JSON file that lists all game assets (sounds, textures, etc.).
///     Corresponds to the "assetIndex" object in the Minecraft version manifest.
/// </summary>
public class AssetIndex
{
    /// <summary>
    ///     The ID of the asset index, typically matching the Minecraft version ID
    ///     or a specific asset version (e.g., "1.20", "legacy").
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    ///     The SHA1 checksum of the asset index JSON file itself.
    /// </summary>
    [JsonPropertyName("sha1")]
    public required string Sha1 { get; set; }

    /// <summary>
    ///     The size of the asset index JSON file in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public ulong Size { get; set; } // C++ used size_t, ulong is a safe mapping for potentially large files

    /// <summary>
    ///     The total size of all assets listed within this asset index, in bytes.
    ///     This is the sum of the sizes of all individual asset files.
    /// </summary>
    [JsonPropertyName("totalSize")]
    public ulong TotalSize { get; set; } // C++ used size_t

    /// <summary>
    ///     The URL from which the asset index JSON file can be downloaded.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }
}