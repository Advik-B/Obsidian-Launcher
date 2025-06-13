using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

/// <summary>
/// Represents the metadata for a single Forge version build from a metadata API.
/// </summary>
public class ForgeVersionInfo
{
    [JsonPropertyName("mcversion")]
    public string MinecraftVersion { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("build")]
    public int Build { get; set; }

    [JsonPropertyName("modified")]
    public DateTime Modified { get; set; }
    
    [JsonPropertyName("files")]
    public List<ForgeFile> Files { get; set; }
}