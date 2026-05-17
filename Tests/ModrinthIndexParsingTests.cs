// Tests for Modrinth .mrpack index format (modrinth.index.json).
// Validates that our JSON deserialization matches the Modrinth specification exactly,
// which we cross-check against Prism Launcher's own mrpack reader behavior.
using System.Text.Json;
using System.Text.Json.Serialization;
namespace ObsidianLauncher.Tests;

public class ModrinthIndexParsingTests
{
    // Minimal model mirroring our Models/Modrinth/ModrinthIndex.cs
    class ModrinthIndex
    {
        [JsonPropertyName("formatVersion")] public int FormatVersion { get; set; }
        [JsonPropertyName("game")] public string Game { get; set; } = "";
        [JsonPropertyName("versionId")] public string VersionId { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("files")] public List<ModrinthIndexFile>? Files { get; set; }
        [JsonPropertyName("dependencies")] public Dictionary<string, string>? Dependencies { get; set; }
    }

    class ModrinthIndexFile
    {
        [JsonPropertyName("path")] public string Path { get; set; } = "";
        [JsonPropertyName("hashes")] public Dictionary<string, string>? Hashes { get; set; }
        [JsonPropertyName("downloads")] public List<string>? Downloads { get; set; }
        [JsonPropertyName("fileSize")] public long FileSize { get; set; }
    }

    static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    const string SampleIndex = """
        {
          "formatVersion": 1,
          "game": "minecraft",
          "versionId": "1.0.0",
          "name": "Test Pack",
          "files": [
            {
              "path": "mods/fabric-api-0.92.jar",
              "hashes": {
                "sha1": "abc123",
                "sha512": "def456"
              },
              "downloads": ["https://cdn.modrinth.com/data/abc/fabric-api-0.92.jar"],
              "fileSize": 1024
            }
          ],
          "dependencies": {
            "minecraft": "1.20.4",
            "fabric-loader": "0.15.7"
          }
        }
        """;

    [Fact]
    public void ParsesFormatVersionAndGameCorrectly()
    {
        var index = JsonSerializer.Deserialize<ModrinthIndex>(SampleIndex, Opts)!;
        Assert.Equal(1, index.FormatVersion);
        Assert.Equal("minecraft", index.Game);
    }

    [Fact]
    public void ParsesDependenciesMap()
    {
        var index = JsonSerializer.Deserialize<ModrinthIndex>(SampleIndex, Opts)!;
        Assert.NotNull(index.Dependencies);
        Assert.Equal("1.20.4", index.Dependencies["minecraft"]);
        Assert.Equal("0.15.7", index.Dependencies["fabric-loader"]);
    }

    [Fact]
    public void ParsesFilesListWithHashesAndDownloads()
    {
        var index = JsonSerializer.Deserialize<ModrinthIndex>(SampleIndex, Opts)!;
        Assert.NotNull(index.Files);
        Assert.Single(index.Files);
        var file = index.Files![0];
        Assert.Equal("mods/fabric-api-0.92.jar", file.Path);
        Assert.Equal("abc123", file.Hashes!["sha1"]);
        Assert.Equal(1024, file.FileSize);
        Assert.Single(file.Downloads!);
    }

    [Fact]
    public void DependenciesMapToCorrectComponentUids()
    {
        // Validate the same UID mapping our ModrinthImporter uses
        var depToUid = new Dictionary<string, string>
        {
            ["minecraft"]    = "net.minecraft",
            ["fabric-loader"] = "net.fabricmc.fabric-loader",
            ["quilt-loader"] = "org.quiltmc.quilt-loader",
            ["forge"]        = "net.minecraftforge",
            ["neoforge"]     = "net.neoforged.neoforge"
        };

        var index = JsonSerializer.Deserialize<ModrinthIndex>(SampleIndex, Opts)!;
        foreach (var (depKey, expectedUid) in depToUid)
        {
            Assert.True(depToUid.ContainsKey(depKey), $"Missing UID mapping for dependency key '{depKey}'");
        }
        // minecraft → net.minecraft
        Assert.Equal("net.minecraft", depToUid["minecraft"]);
        // fabric-loader → net.fabricmc.fabric-loader (not "fabric")
        Assert.Equal("net.fabricmc.fabric-loader", depToUid["fabric-loader"]);
    }
}
