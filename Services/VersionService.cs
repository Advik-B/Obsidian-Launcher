// Services/VersionService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using Serilog;

namespace ObsidianLauncher.Services;

/// <summary>
/// Service for fetching and managing Minecraft version information from the official Mojang API.
/// </summary>
public class VersionService
{
    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;
    private VersionManifest? _cachedManifest;
    private DateTime? _lastFetch;
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(30); // Cache for 30 minutes

    public VersionService(HttpManager httpManager)
    {
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _logger = Log.ForContext<VersionService>();
    }

    /// <summary>
    /// Fetches the latest version manifest from Mojang's API.
    /// Uses caching to avoid excessive API calls.
    /// </summary>
    /// <param name="forceRefresh">If true, ignores cache and fetches fresh data</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The version manifest containing all available versions</returns>
    public async Task<VersionManifest?> GetVersionManifestAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        try
        {
            // Return cached data if available and not expired
            if (!forceRefresh && _cachedManifest != null && _lastFetch.HasValue && 
                DateTime.UtcNow - _lastFetch.Value < _cacheTimeout)
            {
                _logger.Debug("Returning cached version manifest with {Count} versions", _cachedManifest.Versions.Count);
                return _cachedManifest;
            }

            _logger.Information("Fetching Minecraft version manifest from Mojang API...");
            
            var response = await _httpManager.GetAsync(
                "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json",
                cancellationToken: cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Error("Failed to fetch version manifest. Status: {StatusCode}", response.StatusCode);
                return _cachedManifest; // Return cached data if available, or null
            }

            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
            var manifest = JsonSerializer.Deserialize<VersionManifest>(jsonString,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (manifest?.Versions == null)
            {
                _logger.Error("Failed to parse version manifest or no versions found");
                return _cachedManifest; // Return cached data if available, or null
            }

            // Cache the result
            _cachedManifest = manifest;
            _lastFetch = DateTime.UtcNow;

            _logger.Information("Successfully fetched version manifest with {Count} versions", manifest.Versions.Count);
            return manifest;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error fetching version manifest");
            return _cachedManifest; // Return cached data if available, or null
        }
    }

    /// <summary>
    /// Gets a list of version IDs filtered by type (release, snapshot, etc.).
    /// </summary>
    /// <param name="includeReleases">Include release versions</param>
    /// <param name="includeSnapshots">Include snapshot versions</param>
    /// <param name="includeBetas">Include beta versions</param>
    /// <param name="includeAlphas">Include alpha versions</param>
    /// <param name="limit">Maximum number of versions to return (0 = no limit)</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>List of version IDs</returns>
    public async Task<List<string>> GetVersionsAsync(
        bool includeReleases = true,
        bool includeSnapshots = true, 
        bool includeBetas = false,
        bool includeAlphas = false,
        int limit = 0,
        CancellationToken cancellationToken = default)
    {
        var manifest = await GetVersionManifestAsync(cancellationToken: cancellationToken);
        if (manifest?.Versions == null)
        {
            _logger.Warning("No version manifest available, returning empty list");
            return new List<string>();
        }

        var filteredVersions = manifest.Versions.Where(v =>
        {
            return v.Type?.ToLowerInvariant() switch
            {
                "release" => includeReleases,
                "snapshot" => includeSnapshots,
                "beta" => includeBetas,
                "alpha" => includeAlphas,
                _ => false
            };
        });

        var versionIds = filteredVersions.Select(v => v.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();

        if (limit > 0 && versionIds.Count > limit)
        {
            versionIds = versionIds.Take(limit).ToList();
        }

        _logger.Debug("Filtered {Count} versions from manifest", versionIds.Count);
        return versionIds;
    }

    /// <summary>
    /// Gets the latest release version ID.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The latest release version ID, or null if not available</returns>
    public async Task<string?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await GetVersionManifestAsync(cancellationToken: cancellationToken);
        return manifest?.Latest?.Release;
    }

    /// <summary>
    /// Gets the latest snapshot version ID.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The latest snapshot version ID, or null if not available</returns>
    public async Task<string?> GetLatestSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await GetVersionManifestAsync(cancellationToken: cancellationToken);
        return manifest?.Latest?.Snapshot;
    }

    /// <summary>
    /// Clears the cached version manifest, forcing a fresh fetch on the next request.
    /// </summary>
    public void ClearCache()
    {
        _cachedManifest = null;
        _lastFetch = null;
        _logger.Debug("Version manifest cache cleared");
    }
}