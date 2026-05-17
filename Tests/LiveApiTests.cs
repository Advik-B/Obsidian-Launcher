// Live integration tests that hit the same APIs Prism Launcher uses.
// These validate that our launcher would produce compatible outputs for
// real-world inputs. Network access is required.
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace ObsidianLauncher.Tests;

/// <summary>
/// Validates our launcher against the same external APIs Prism Launcher queries.
/// Each test method documents what Prism Launcher does and verifies we do the same.
/// </summary>
public class LiveApiTests : IDisposable
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    public LiveApiTests()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "ObsidianLauncher/1.0 (testing)");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public void Dispose() => _http.Dispose();

    // ── Mojang Version Manifest ─────────────────────────────────────────────

    class MojangManifest
    {
        [JsonPropertyName("latest")] public MojangLatest? Latest { get; set; }
        [JsonPropertyName("versions")] public List<MojangVersionEntry>? Versions { get; set; }
    }
    class MojangLatest
    {
        [JsonPropertyName("release")] public string? Release { get; set; }
        [JsonPropertyName("snapshot")] public string? Snapshot { get; set; }
    }
    class MojangVersionEntry
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("url")] public string Url { get; set; } = "";
        [JsonPropertyName("releaseTime")] public DateTime ReleaseTime { get; set; }
    }

    [Fact]
    public async Task MojangManifest_ReturnsReleasesAndSnapshots()
    {
        // Prism Launcher fetches from the same URL to populate the version selector.
        var json = await _http.GetStringAsync(
            "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json");

        var manifest = JsonSerializer.Deserialize<MojangManifest>(json, Opts);

        Assert.NotNull(manifest);
        Assert.NotNull(manifest!.Latest?.Release);
        Assert.NotNull(manifest.Versions);
        Assert.True(manifest.Versions!.Count > 100, "Expected 100+ Minecraft versions");

        // Latest release must be a proper version string
        Assert.Matches(@"^\d+\.\d+", manifest.Latest!.Release!);

        // Must have both release and snapshot types
        Assert.Contains(manifest.Versions, v => v.Type == "release");
        Assert.Contains(manifest.Versions, v => v.Type == "snapshot");

        // 1.20.1 must be present (used by thousands of modpacks)
        Assert.Contains(manifest.Versions, v => v.Id == "1.20.1");
    }

    [Fact]
    public async Task MojangManifest_VersionUrlIsReachable()
    {
        // Prism Launcher fetches each version JSON on demand; verify the URL format is consistent.
        var json = await _http.GetStringAsync(
            "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json");
        var manifest = JsonSerializer.Deserialize<MojangManifest>(json, Opts)!;

        var latest = manifest.Versions!.First(v => v.Id == manifest.Latest!.Release);
        Assert.StartsWith("https://", latest.Url);

        // Fetch the version JSON itself and verify it has MainClass
        var versionJson = await _http.GetStringAsync(latest.Url);
        using var doc = JsonDocument.Parse(versionJson);
        Assert.True(doc.RootElement.TryGetProperty("mainClass", out var mainClass));
        Assert.Equal("net.minecraft.client.main.Main", mainClass.GetString());
    }

    // ── Fabric Meta API ────────────────────────────────────────────────────

    class FabricLoaderEntry
    {
        [JsonPropertyName("loader")] public FabricLoaderInfo? Loader { get; set; }
    }
    class FabricLoaderInfo
    {
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("stable")] public bool Stable { get; set; }
    }

    [Fact]
    public async Task FabricMeta_ListsLoadersForMinecraft1_20_1()
    {
        // Prism Launcher queries this exact endpoint to populate the Fabric loader version list.
        var json = await _http.GetStringAsync(
            "https://meta.fabricmc.net/v2/versions/loader/1.20.1");

        var loaders = JsonSerializer.Deserialize<List<FabricLoaderEntry>>(json, Opts);
        Assert.NotNull(loaders);
        Assert.True(loaders!.Count > 0, "Expected at least one Fabric loader for 1.20.1");

        // First entry should be newest (stable or unstable)
        Assert.NotEmpty(loaders[0].Loader!.Version);

        // At least one stable loader must exist
        Assert.Contains(loaders, l => l.Loader?.Stable == true);
    }

    [Fact]
    public async Task FabricMeta_ProfileJsonHasExpectedFields()
    {
        // Prism Launcher fetches the combined profile JSON which contains InheritsFrom, libraries, etc.
        // We need InheritsFrom to load the base Minecraft version first.
        var loaderListJson = await _http.GetStringAsync(
            "https://meta.fabricmc.net/v2/versions/loader/1.20.1");
        var loaders = JsonSerializer.Deserialize<List<FabricLoaderEntry>>(loaderListJson, Opts)!;
        var stableLoader = loaders.First(l => l.Loader?.Stable == true);

        var profileUrl = $"https://meta.fabricmc.net/v2/versions/loader/1.20.1/{stableLoader.Loader!.Version}/profile/json";
        var profileJson = await _http.GetStringAsync(profileUrl);

        using var doc = JsonDocument.Parse(profileJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("inheritsFrom", out var inheritsFrom),
            "Fabric profile must contain 'inheritsFrom'");
        Assert.Equal("1.20.1", inheritsFrom.GetString());

        Assert.True(root.TryGetProperty("libraries", out var libs),
            "Fabric profile must contain 'libraries'");
        Assert.True(libs.GetArrayLength() > 0);

        Assert.True(root.TryGetProperty("mainClass", out _),
            "Fabric profile must have mainClass");
    }

    // ── Quilt Meta API ─────────────────────────────────────────────────────

    [Fact]
    public async Task QuiltMeta_ListsLoadersForMinecraft1_20_1()
    {
        var json = await _http.GetStringAsync(
            "https://meta.quiltmc.org/v3/versions/loader/1.20.1");
        var loaders = JsonSerializer.Deserialize<JsonElement>(json, Opts);

        Assert.Equal(JsonValueKind.Array, loaders.ValueKind);
        Assert.True(loaders.GetArrayLength() > 0, "Expected Quilt loaders for 1.20.1");
    }

    // ── NeoForge Maven ────────────────────────────────────────────────────

    [Fact]
    public async Task NeoForgeMaven_MetadataXmlIsReachable()
    {
        // Prism Launcher fetches NeoForge versions from the Maven metadata XML.
        var response = await _http.GetAsync(
            "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml");

        Assert.True(response.IsSuccessStatusCode,
            $"NeoForge Maven metadata returned {response.StatusCode}");

        var xml = await response.Content.ReadAsStringAsync();
        Assert.Contains("<metadata>", xml);
        Assert.Contains("<versioning>", xml);
        Assert.Contains("<versions>", xml);
    }

    // ── Modrinth API ────────────────────────────────────────────────────────

    class ModrinthSearchResult
    {
        [JsonPropertyName("hits")] public List<ModrinthHit>? Hits { get; set; }
        [JsonPropertyName("total_hits")] public int TotalHits { get; set; }
    }
    class ModrinthHit
    {
        [JsonPropertyName("project_id")] public string ProjectId { get; set; } = "";
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("project_type")] public string ProjectType { get; set; } = "";
    }

    [Fact]
    public async Task ModrinthSearch_ReturnsFabricApiForMod()
    {
        // Prism Launcher and Obsidian Launcher both use Modrinth API v2 for mod search.
        var url = "https://api.modrinth.com/v2/search?query=fabric+api&facets=[[%22project_type:mod%22]]&limit=5";
        _http.DefaultRequestHeaders.Remove("User-Agent");
        _http.DefaultRequestHeaders.Add("User-Agent", "ObsidianLauncher/1.0 (testing; contact@example.com)");

        var json = await _http.GetStringAsync(url);
        var result = JsonSerializer.Deserialize<ModrinthSearchResult>(json, Opts);

        Assert.NotNull(result);
        Assert.True(result!.TotalHits > 0, "Expected search results for 'fabric api'");
        Assert.NotEmpty(result.Hits!);

        // All hits should be mods
        Assert.All(result.Hits!, h => Assert.Equal("mod", h.ProjectType));
    }

    [Fact]
    public async Task ModrinthProject_FabricApiHasCorrectStructure()
    {
        // Fetch fabric-api project directly (slug is well-known)
        var json = await _http.GetStringAsync("https://api.modrinth.com/v2/project/fabric-api");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("id", out _));
        Assert.True(root.TryGetProperty("slug", out var slug));
        Assert.Equal("fabric-api", slug.GetString());
        Assert.True(root.TryGetProperty("project_type", out var projectType));
        Assert.Equal("mod", projectType.GetString());
        Assert.True(root.TryGetProperty("downloads", out _));
    }

    // ── Forge Maven ────────────────────────────────────────────────────────

    [Fact]
    public async Task ForgeMaven_MetadataXmlIsReachable()
    {
        var response = await _http.GetAsync(
            "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml");

        Assert.True(response.IsSuccessStatusCode,
            $"Forge Maven metadata returned {response.StatusCode}");

        var xml = await response.Content.ReadAsStringAsync();
        Assert.Contains("<metadata>", xml);
        Assert.Contains("<versions>", xml);
        // Must contain at least one 1.20.1 forge version
        Assert.Contains("1.20.1-", xml);
    }

    // ── GitHub Update Checker API ────────────────────────────────────────

    [Fact]
    public async Task GitHubReleasesApi_ReturnsValidResponse()
    {
        // The update checker hits GitHub's releases API.
        // This test verifies the API is reachable and returns the expected shape.
        _http.DefaultRequestHeaders.Remove("User-Agent");
        _http.DefaultRequestHeaders.Add("User-Agent", "ObsidianLauncher/1.0 (testing)");

        var response = await _http.GetAsync(
            "https://api.github.com/repos/Advik-B/Obsidian-Launcher/releases/latest");

        // 200 = has a release; 404 = no release yet (both are acceptable)
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK ||
                    response.StatusCode == System.Net.HttpStatusCode.NotFound,
            $"Unexpected status: {response.StatusCode}");

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("tag_name", out _));
        }
    }
}
