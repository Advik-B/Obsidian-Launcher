using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services.ModLoaders;

/// <summary>
/// Fetches and parses Quilt mod loader profile JSON from the QuiltMC meta server.
/// The Quilt meta API mirrors the Fabric meta structure.
/// </summary>
public class QuiltInstaller
{
    private const string MetaBase = "https://meta.quiltmc.org/v3";
    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public QuiltInstaller(HttpManager httpManager)
    {
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _logger = LogHelper.GetLogger<QuiltInstaller>();
    }

    /// <summary>
    /// Fetches the combined Minecraft + Quilt Loader launch profile JSON.
    /// </summary>
    public async Task<MinecraftVersion?> GetProfileAsync(
        string gameVersion,
        string loaderVersion,
        CancellationToken ct = default)
    {
        var url = $"{MetaBase}/versions/loader/{Uri.EscapeDataString(gameVersion)}/{Uri.EscapeDataString(loaderVersion)}/profile/json";
        _logger.Information("Fetching Quilt profile: game={Game} loader={Loader}", gameVersion, loaderVersion);

        var response = await _httpManager.GetAsync(url, cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Failed to fetch Quilt profile: {Status} from {Url}", response.StatusCode, url);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var profile = JsonSerializer.Deserialize<MinecraftVersion>(json, JsonOptions);
        if (profile != null)
            _logger.Information("Quilt profile fetched: id={Id} mainClass={MainClass}", profile.Id, profile.MainClass);
        return profile;
    }

    /// <summary>
    /// Get available Quilt loader versions for a given game version.
    /// </summary>
    public async Task<QuiltLoaderVersionList?> GetLoadersForGameVersionAsync(string gameVersion, CancellationToken ct = default)
    {
        var url = $"{MetaBase}/versions/loader/{Uri.EscapeDataString(gameVersion)}";
        var response = await _httpManager.GetAsync(url, cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Failed to fetch Quilt loader list: {Status}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<QuiltLoaderVersionList>(json, JsonOptions);
    }
}

public class QuiltLoaderVersionList : System.Collections.Generic.List<QuiltLoaderEntry> { }

public class QuiltLoaderEntry
{
    public QuiltLoaderInfo? Loader { get; set; }
    public QuiltIntermediary? Intermediary { get; set; }
}

public class QuiltLoaderInfo
{
    public string? Version { get; set; }
    public bool Stable { get; set; }
}

public class QuiltIntermediary
{
    public string? Version { get; set; }
}
