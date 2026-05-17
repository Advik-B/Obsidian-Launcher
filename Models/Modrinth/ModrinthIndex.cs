using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models.Modrinth;

/// <summary>
/// Represents the modrinth.index.json found inside a .mrpack modpack.
/// </summary>
public class ModrinthIndex
{
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; }

    [JsonPropertyName("game")]
    public string Game { get; set; } = "minecraft";

    [JsonPropertyName("versionId")]
    public string VersionId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("files")]
    public List<ModrinthIndexFile> Files { get; set; } = new();

    [JsonPropertyName("dependencies")]
    public Dictionary<string, string> Dependencies { get; set; } = new();
}

public class ModrinthIndexFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("hashes")]
    public ModrinthHashes Hashes { get; set; } = new();

    [JsonPropertyName("env")]
    public ModrinthEnv? Env { get; set; }

    [JsonPropertyName("downloads")]
    public List<string> Downloads { get; set; } = new();

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }
}

public class ModrinthEnv
{
    [JsonPropertyName("client")]
    public string Client { get; set; } = "required";

    [JsonPropertyName("server")]
    public string Server { get; set; } = "required";
}
