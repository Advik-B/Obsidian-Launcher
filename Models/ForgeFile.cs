using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

/// <summary>
/// Represents a single file within the metadata for a Forge version from a metadata API.
/// </summary>
public class ForgeFile
{
    [JsonPropertyName("format")]
    public string Format { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; }
}