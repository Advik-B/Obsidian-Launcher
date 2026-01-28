using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services;

public class ModLoaderService
{
    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;

    public ModLoaderService(HttpManager httpManager)
    {
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _logger = LogHelper.GetLogger<ModLoaderService>();
    }

    /// <summary>
    /// Fetches the version-specific JSON for a given mod loader.
    /// </summary>
    public async Task<MinecraftVersion?> GetModLoaderVersionAsync(Component component, CancellationToken cancellationToken = default)
    {
        _logger.Information("Fetching version details for mod loader: {Uid} {Version}", component.Uid, component.Version);

        string? url = component.Uid switch
        {
            "net.fabricmc.fabric-loader" => $"https://meta.fabricmc.net/v2/versions/loader/{component.Version}/{component.Version}/profile/json",
            // TODO: Add cases for Forge, Quilt, NeoForge
            "net.minecraftforge" => null, // Requires more complex logic, often parsing a Maven repo.
            _ => null
        };

        if (string.IsNullOrEmpty(url))
        {
            _logger.Error("Unsupported mod loader UID '{Uid}' for automatic metadata fetching.", component.Uid);
            return null;
        }

        try
        {
            var response = await _httpManager.GetAsync(url, cancellationToken: cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Error("Failed to fetch mod loader manifest for {Uid} {Version}. Status: {StatusCode}, URL: {Url}",
                    component.Uid, component.Version, response.StatusCode, url);
                return null;
            }

            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
            var mcVersion = JsonSerializer.Deserialize<MinecraftVersion>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            _logger.Information("Successfully fetched and parsed manifest for {Uid} {Version}.", component.Uid, component.Version);
            return mcVersion;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception while fetching manifest for {Uid} {Version} from {Url}", component.Uid, component.Version, url);
            return null;
        }
    }
}