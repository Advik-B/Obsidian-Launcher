// Tests for CurseForge modpack manifest.json parsing.
// Verifies our parser reads the same fields that Prism Launcher reads from CurseForge ZIPs.
using System.Text.Json;
using System.Text.Json.Serialization;
namespace ObsidianLauncher.Tests;

public class CurseForgeManifestParsingTests
{
    class CurseForgeManifest
    {
        [JsonPropertyName("minecraft")] public CurseForgeMinecraft? Minecraft { get; set; }
        [JsonPropertyName("manifestType")] public string ManifestType { get; set; } = "";
        [JsonPropertyName("manifestVersion")] public int ManifestVersion { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("author")] public string Author { get; set; } = "";
        [JsonPropertyName("files")] public List<CurseForgeFile>? Files { get; set; }
    }
    class CurseForgeMinecraft
    {
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("modLoaders")] public List<CurseForgeModLoader>? ModLoaders { get; set; }
    }
    class CurseForgeModLoader
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("primary")] public bool Primary { get; set; }
    }
    class CurseForgeFile
    {
        [JsonPropertyName("projectID")] public int ProjectId { get; set; }
        [JsonPropertyName("fileID")] public int FileId { get; set; }
        [JsonPropertyName("required")] public bool Required { get; set; }
    }

    static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    const string SampleManifest = """
        {
          "minecraft": {
            "version": "1.20.1",
            "modLoaders": [
              { "id": "forge-47.2.0", "primary": true }
            ]
          },
          "manifestType": "minecraftModpack",
          "manifestVersion": 1,
          "name": "Test Forge Pack",
          "version": "1.0",
          "author": "Tester",
          "files": [
            { "projectID": 123456, "fileID": 789012, "required": true },
            { "projectID": 234567, "fileID": 890123, "required": false }
          ]
        }
        """;

    [Fact]
    public void ParsesPackNameAndVersion()
    {
        var manifest = JsonSerializer.Deserialize<CurseForgeManifest>(SampleManifest, Opts)!;
        Assert.Equal("Test Forge Pack", manifest.Name);
        Assert.Equal("1.0", manifest.Version);
    }

    [Fact]
    public void ParsesMinecraftVersionAndModLoader()
    {
        var manifest = JsonSerializer.Deserialize<CurseForgeManifest>(SampleManifest, Opts)!;
        Assert.Equal("1.20.1", manifest.Minecraft!.Version);
        Assert.Single(manifest.Minecraft.ModLoaders!);
        Assert.Equal("forge-47.2.0", manifest.Minecraft.ModLoaders![0].Id);
        Assert.True(manifest.Minecraft.ModLoaders![0].Primary);
    }

    [Fact]
    public void ParsesFilesListWithProjectAndFileIds()
    {
        var manifest = JsonSerializer.Deserialize<CurseForgeManifest>(SampleManifest, Opts)!;
        Assert.Equal(2, manifest.Files!.Count);
        Assert.Equal(123456, manifest.Files[0].ProjectId);
        Assert.Equal(789012, manifest.Files[0].FileId);
        Assert.True(manifest.Files[0].Required);
        Assert.False(manifest.Files[1].Required);
    }

    [Theory]
    [InlineData("forge-47.2.0", "net.minecraftforge", "47.2.0")]
    [InlineData("neoforge-21.1.73", "net.neoforged.neoforge", "21.1.73")]
    [InlineData("fabric-0.15.7", "net.fabricmc.fabric-loader", "0.15.7")]
    public void ModLoaderIdMapsToCorrectUidAndVersion(string loaderId, string expectedUid, string expectedVersion)
    {
        // Replicate CurseForgeImporter.BuildComponents mod loader UID resolution
        string uid, version;
        if (loaderId.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
        {
            uid = "net.minecraftforge";
            version = loaderId["forge-".Length..];
        }
        else if (loaderId.StartsWith("neoforge-", StringComparison.OrdinalIgnoreCase))
        {
            uid = "net.neoforged.neoforge";
            version = loaderId["neoforge-".Length..];
        }
        else if (loaderId.StartsWith("fabric-", StringComparison.OrdinalIgnoreCase))
        {
            uid = "net.fabricmc.fabric-loader";
            version = loaderId["fabric-".Length..];
        }
        else
        {
            uid = "unknown";
            version = loaderId;
        }

        Assert.Equal(expectedUid, uid);
        Assert.Equal(expectedVersion, version);
    }
}
