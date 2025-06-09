using System;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

public class VersionMetadata
{
    [JsonPropertyName("id")] public required string Id { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; } // "release", "snapshot", "old_beta", "old_alpha"

    [JsonPropertyName("url")] public required string Url { get; set; } // URL to the version-specific JSON

    [JsonPropertyName("time")] public DateTime Time { get; set; } // Last modification time of this entry

    [JsonPropertyName("releaseTime")] public DateTime ReleaseTime { get; set; } // Actual release time of the version

    [JsonPropertyName("sha1")] public required string Sha1 { get; set; } // SHA1 of the version-specific JSON file

    [JsonPropertyName("complianceLevel")] public int ComplianceLevel { get; set; }
}