using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models.Modrinth;

public class ModrinthVersion
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version_number")]
    public string VersionNumber { get; set; } = string.Empty;

    [JsonPropertyName("changelog")]
    public string? Changelog { get; set; }

    [JsonPropertyName("version_type")]
    public string VersionType { get; set; } = "release";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "listed";

    [JsonPropertyName("loaders")]
    public List<string> Loaders { get; set; } = new();

    [JsonPropertyName("game_versions")]
    public List<string> GameVersions { get; set; } = new();

    [JsonPropertyName("files")]
    public List<ModrinthVersionFile> Files { get; set; } = new();

    [JsonPropertyName("dependencies")]
    public List<ModrinthDependency> Dependencies { get; set; } = new();

    [JsonPropertyName("date_published")]
    public string? DatePublished { get; set; }

    [JsonPropertyName("downloads")]
    public long Downloads { get; set; }
}

public class ModrinthVersionFile
{
    [JsonPropertyName("hashes")]
    public ModrinthHashes Hashes { get; set; } = new();

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("file_type")]
    public string? FileType { get; set; }
}

public class ModrinthHashes
{
    [JsonPropertyName("sha512")]
    public string? Sha512 { get; set; }

    [JsonPropertyName("sha1")]
    public string? Sha1 { get; set; }
}

public class ModrinthDependency
{
    [JsonPropertyName("project_id")]
    public string? ProjectId { get; set; }

    [JsonPropertyName("version_id")]
    public string? VersionId { get; set; }

    [JsonPropertyName("dependency_type")]
    public string DependencyType { get; set; } = "required";
}
