using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services;

/// <summary>
/// Fetches Forge version metadata from a third-party API.
/// </summary>
public class ForgeMetadataService
{
    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;
    private const string ApiBaseUrl = "https://bmclapi2.bangbang93.com/forge/minecraft";

    public ForgeMetadataService(HttpManager httpManager)
    {
        _httpManager = httpManager;
        _logger = LogHelper.GetLogger<ForgeMetadataService>();
    }

    /// <summary>
    /// Gets a list of available Forge versions for a specific Minecraft version.
    /// </summary>
    /// <param name="minecraftVersion">The Minecraft version to query (e.g., "1.16.1").</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of ForgeVersionInfo objects, or null on failure.</returns>
    public async Task<List<ForgeVersionInfo>> GetForgeVersionsAsync(string minecraftVersion, CancellationToken cancellationToken = default)
    {
        var requestUrl = $"{ApiBaseUrl}/{minecraftVersion}";
        _logger.Information("Fetching Forge version list from: {Url}", requestUrl);

        var response = await _httpManager.GetAsync(requestUrl, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Failed to fetch Forge metadata from API. Status: {StatusCode}, URL: {Url}", response.StatusCode, requestUrl);
            return null;
        }

        try
        {
            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
            var versions = JsonSerializer.Deserialize<List<ForgeVersionInfo>>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            // Sort the versions by build number descending to show the newest first
            return versions?.OrderByDescending(v => v.Build).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to parse Forge metadata response from API.");
            return null;
        }
    }
}