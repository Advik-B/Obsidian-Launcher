using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services.ModLoaders;

/// <summary>
/// Fetches and parses Fabric mod loader profile JSON from the FabricMC meta server.
/// </summary>
public class FabricInstaller
{
    private const string MetaBase = "https://meta.fabricmc.net/v2";
    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FabricInstaller(HttpManager httpManager)
    {
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _logger = LogHelper.GetLogger<FabricInstaller>();
    }

    /// <summary>
    /// Fetches the combined Minecraft + Fabric Loader launch profile JSON.
    /// </summary>
    public async Task<MinecraftVersion?> GetProfileAsync(
        string gameVersion,
        string loaderVersion,
        CancellationToken ct = default)
    {
        var url = $"{MetaBase}/versions/loader/{Uri.EscapeDataString(gameVersion)}/{Uri.EscapeDataString(loaderVersion)}/profile/json";
        _logger.Information("Fetching Fabric profile: game={Game} loader={Loader}", gameVersion, loaderVersion);

        var response = await _httpManager.GetAsync(url, cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Failed to fetch Fabric profile: {Status} from {Url}", response.StatusCode, url);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var profile = JsonSerializer.Deserialize<MinecraftVersion>(json, JsonOptions);
        if (profile != null)
            _logger.Information("Fabric profile fetched: id={Id} mainClass={MainClass}", profile.Id, profile.MainClass);
        return profile;
    }
}
