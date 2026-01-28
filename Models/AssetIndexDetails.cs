using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

/// <summary>
///     Represents the detailed content of an asset index JSON file.
///     This file lists all individual game assets and their properties.
/// </summary>
public class AssetIndexDetails
{
    public AssetIndexDetails()
    {
        Objects = new Dictionary<string, AssetObjectInfo>();
    }

    /// <summary>
    ///     A dictionary where the key is the virtual path of the asset (e.g., "minecraft/textures/block/stone.png")
    ///     and the value is an <see cref="AssetObjectInfo" /> containing the hash and size of the asset.
    /// </summary>
    [JsonPropertyName("objects")]
    public Dictionary<string, AssetObjectInfo> Objects { get; set; }

    /// <summary>
    ///     Indicates if this asset index is for a "virtual" or "legacy" asset set,
    ///     which might require special handling for storage paths.
    ///     (Not always present, defaults to false if missing).
    /// </summary>
    [JsonPropertyName("virtual")]
    public bool IsVirtual { get; set; } = false; // Default to false if not present

    /// <summary>
    ///     Indicates if this asset index maps files to the legacy directory structure.
    ///     (Not always present, defaults to false if missing).
    ///     This is relevant for very old Minecraft versions (pre-1.7.3).
    /// </summary>
    [JsonPropertyName("map_to_resources")]
    public bool MapToResources { get; set; } = false; // Default to false if not present
}