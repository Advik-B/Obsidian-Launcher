using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models.Forge;

/// <summary>
/// Represents the install_profile.json from a Forge installer JAR.
/// Covers both old format (1.12.2 and below) and new format (1.13+).
/// </summary>
public class ForgeInstallProfile
{
    // New format (1.13+)
    [JsonPropertyName("spec")]
    public int? Spec { get; set; }

    [JsonPropertyName("profile")]
    public string? Profile { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("minecraft")]
    public string? Minecraft { get; set; }

    [JsonPropertyName("serverJarPath")]
    public string? ServerJarPath { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, ForgeDataEntry>? Data { get; set; }

    [JsonPropertyName("processors")]
    public List<ForgeProcessor>? Processors { get; set; }

    [JsonPropertyName("libraries")]
    public List<ForgeLibrary>? Libraries { get; set; }

    // Old format (1.12.2 and below)
    [JsonPropertyName("install")]
    public ForgeInstallInfo? Install { get; set; }

    [JsonPropertyName("versionInfo")]
    public ForgeVersionInfo? VersionInfo { get; set; }

    public bool IsNewFormat => Spec != null || (Profile != null && Processors != null);
}

public class ForgeDataEntry
{
    [JsonPropertyName("client")]
    public string? Client { get; set; }

    [JsonPropertyName("server")]
    public string? Server { get; set; }
}

public class ForgeProcessor
{
    [JsonPropertyName("sides")]
    public List<string>? Sides { get; set; }

    [JsonPropertyName("jar")]
    public string? Jar { get; set; }

    [JsonPropertyName("classpath")]
    public List<string>? Classpath { get; set; }

    [JsonPropertyName("args")]
    public List<string>? Args { get; set; }

    [JsonPropertyName("outputs")]
    public Dictionary<string, string>? Outputs { get; set; }
}

public class ForgeLibrary
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("downloads")]
    public ForgeLibraryDownloads? Downloads { get; set; }
}

public class ForgeLibraryDownloads
{
    [JsonPropertyName("artifact")]
    public ForgeArtifact? Artifact { get; set; }
}

public class ForgeArtifact
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("sha1")]
    public string? Sha1 { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

// Old format models
public class ForgeInstallInfo
{
    [JsonPropertyName("profileName")]
    public string? ProfileName { get; set; }

    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    [JsonPropertyName("welcome")]
    public string? Welcome { get; set; }

    [JsonPropertyName("minecraft")]
    public string? Minecraft { get; set; }

    [JsonPropertyName("mirrorList")]
    public string? MirrorList { get; set; }

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}

public class ForgeVersionInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("releaseTime")]
    public string? ReleaseTime { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("minecraftArguments")]
    public string? MinecraftArguments { get; set; }

    [JsonPropertyName("mainClass")]
    public string? MainClass { get; set; }

    [JsonPropertyName("inheritsFrom")]
    public string? InheritsFrom { get; set; }

    [JsonPropertyName("jar")]
    public string? Jar { get; set; }

    [JsonPropertyName("libraries")]
    public List<ForgeLibraryOld>? Libraries { get; set; }
}

public class ForgeLibraryOld
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("checksums")]
    public List<string>? Checksums { get; set; }

    [JsonPropertyName("serverreq")]
    public bool ServerReq { get; set; }

    [JsonPropertyName("clientreq")]
    public bool? ClientReq { get; set; }
}
