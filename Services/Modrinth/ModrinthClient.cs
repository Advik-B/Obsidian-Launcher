using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models.Modrinth;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services.Modrinth;

/// <summary>
/// Client for the Modrinth API v2.
/// </summary>
public class ModrinthClient
{
    private const string BaseUrl = "https://api.modrinth.com/v2";
    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ModrinthClient(HttpManager httpManager)
    {
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _logger = LogHelper.GetLogger<ModrinthClient>();
    }

    /// <summary>
    /// Search Modrinth for projects (mods, modpacks, resource packs, etc.).
    /// </summary>
    public async Task<ModrinthSearchResult?> SearchAsync(
        string query,
        string projectType = "mod",
        string? gameVersion = null,
        string? loader = null,
        int limit = 20,
        int offset = 0,
        CancellationToken ct = default)
    {
        var facets = new List<string>();
        facets.Add($"[\"project_type:{projectType}\"]");
        if (!string.IsNullOrEmpty(gameVersion))
            facets.Add($"[\"versions:{gameVersion}\"]");
        if (!string.IsNullOrEmpty(loader))
            facets.Add($"[\"categories:{loader}\"]");

        var facetsStr = "[" + string.Join(",", facets) + "]";
        var url = $"{BaseUrl}/search?query={Uri.EscapeDataString(query)}&facets={Uri.EscapeDataString(facetsStr)}&limit={limit}&offset={offset}";

        _logger.Information("Modrinth search: query={Query} type={Type} game={Game} loader={Loader}", query, projectType, gameVersion, loader);

        var response = await _httpManager.GetAsync(url, cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Modrinth search failed: {StatusCode} for {Url}", response.StatusCode, url);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<ModrinthSearchResult>(json, JsonOptions);
    }

    /// <summary>
    /// Get details for a specific project by slug or ID.
    /// </summary>
    public async Task<ModrinthProject?> GetProjectAsync(string slugOrId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/project/{Uri.EscapeDataString(slugOrId)}";
        var response = await _httpManager.GetAsync(url, cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Modrinth get project failed: {StatusCode} for {SlugOrId}", response.StatusCode, slugOrId);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<ModrinthProject>(json, JsonOptions);
    }

    /// <summary>
    /// Get all versions for a project, optionally filtered by game version and loader.
    /// </summary>
    public async Task<List<ModrinthVersion>?> GetProjectVersionsAsync(
        string slugOrId,
        string? gameVersion = null,
        string? loader = null,
        CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/project/{Uri.EscapeDataString(slugOrId)}/version";
        var queryParts = new List<string>();
        if (!string.IsNullOrEmpty(gameVersion))
            queryParts.Add($"game_versions=[\"{gameVersion}\"]");
        if (!string.IsNullOrEmpty(loader))
            queryParts.Add($"loaders=[\"{loader}\"]");
        if (queryParts.Count > 0)
            url += "?" + string.Join("&", queryParts);

        var response = await _httpManager.GetAsync(url, cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Modrinth get versions failed: {StatusCode} for {SlugOrId}", response.StatusCode, slugOrId);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<ModrinthVersion>>(json, JsonOptions);
    }

    /// <summary>
    /// Get a specific version by ID.
    /// </summary>
    public async Task<ModrinthVersion?> GetVersionAsync(string versionId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/version/{Uri.EscapeDataString(versionId)}";
        var response = await _httpManager.GetAsync(url, cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Modrinth get version failed: {StatusCode} for {VersionId}", response.StatusCode, versionId);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<ModrinthVersion>(json, JsonOptions);
    }

    /// <summary>
    /// Download the primary file for a version to the given destination path.
    /// Returns the download path on success, null on failure.
    /// </summary>
    public async Task<string?> DownloadVersionFileAsync(
        ModrinthVersion version,
        string destinationDir,
        IProgress<float>? progress = null,
        CancellationToken ct = default)
    {
        var primaryFile = version.Files.Find(f => f.Primary) ?? (version.Files.Count > 0 ? version.Files[0] : null);
        if (primaryFile == null)
        {
            _logger.Error("No files found for version {VersionId}", version.Id);
            return null;
        }

        Directory.CreateDirectory(destinationDir);
        var destPath = Path.Combine(destinationDir, primaryFile.Filename);

        if (File.Exists(destPath) && primaryFile.Hashes.Sha1 != null)
        {
            var existingHash = await CryptoUtils.CalculateFileSHA1Async(destPath, ct);
            if (string.Equals(existingHash, primaryFile.Hashes.Sha1, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Information("Modrinth file already cached: {Filename}", primaryFile.Filename);
                return destPath;
            }
        }

        _logger.Information("Downloading Modrinth file: {Filename} from {Url}", primaryFile.Filename, primaryFile.Url);
        var (resp, path) = await _httpManager.DownloadAsync(primaryFile.Url, destPath, progress, ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.Error("Modrinth download failed: {StatusCode}", resp.StatusCode);
            return null;
        }

        if (primaryFile.Hashes.Sha1 != null)
        {
            var actualHash = await CryptoUtils.CalculateFileSHA1Async(destPath, ct);
            if (!string.Equals(actualHash, primaryFile.Hashes.Sha1, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Error("Hash mismatch for {Filename}: expected {Expected}, got {Actual}",
                    primaryFile.Filename, primaryFile.Hashes.Sha1, actualHash);
                File.Delete(destPath);
                return null;
            }
        }

        return destPath;
    }

    /// <summary>
    /// Get multiple versions by their IDs in a single request.
    /// </summary>
    public async Task<List<ModrinthVersion>?> GetVersionsAsync(IEnumerable<string> versionIds, CancellationToken ct = default)
    {
        var ids = string.Join(",", System.Linq.Enumerable.Select(versionIds, id => $"\"{id}\""));
        var url = $"{BaseUrl}/versions?ids=[{ids}]";
        var response = await _httpManager.GetAsync(url, cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<ModrinthVersion>>(json, JsonOptions);
    }
}
