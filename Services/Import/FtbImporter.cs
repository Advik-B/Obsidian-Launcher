using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services.Import;

/// <summary>
/// Imports modpacks from the FTB (Feed the Beast) API.
/// FTB packs are identified by a numeric pack ID and version ID fetched from api.modpacks.ch.
/// </summary>
public class FtbImporter
{
    private const string FtbApiBase = "https://api.modpacks.ch/public";

    private readonly InstanceManager _instanceManager;
    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FtbImporter(InstanceManager instanceManager, HttpManager httpManager)
    {
        _instanceManager = instanceManager ?? throw new ArgumentNullException(nameof(instanceManager));
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _logger = LogHelper.GetLogger<FtbImporter>();
    }

    /// <summary>
    /// Search for FTB modpacks by name.
    /// Returns a list of search result entries.
    /// </summary>
    public async Task<List<FtbPackSummary>> SearchAsync(string query, CancellationToken ct = default)
    {
        var url = $"{FtbApiBase}/modpack/search/50?term={Uri.EscapeDataString(query)}";
        _logger.Information("Searching FTB packs: {Query}", query);

        var response = await _httpManager.GetAsync(url, cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Warning("FTB search failed: {Status}", response.StatusCode);
            return new List<FtbPackSummary>();
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<FtbSearchResult>(json, JsonOptions);
        return result?.Packs?.Select(p => new FtbPackSummary
        {
            Id = p.Id,
            Name = p.Name ?? "",
            Synopsis = p.Synopsis ?? "",
            Installs = p.Installs
        }).ToList() ?? new List<FtbPackSummary>();
    }

    /// <summary>
    /// Fetch full pack metadata including available versions.
    /// </summary>
    public async Task<FtbPackInfo?> GetPackInfoAsync(long packId, CancellationToken ct = default)
    {
        var url = $"{FtbApiBase}/modpack/{packId}";
        _logger.Information("Fetching FTB pack info: {PackId}", packId);

        var response = await _httpManager.GetAsync(url, cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Failed to fetch FTB pack {PackId}: {Status}", packId, response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<FtbPackInfo>(json, JsonOptions);
    }

    /// <summary>
    /// Fetch version manifest for a specific pack version.
    /// </summary>
    public async Task<FtbVersionManifest?> GetVersionManifestAsync(long packId, long versionId, CancellationToken ct = default)
    {
        var url = $"{FtbApiBase}/modpack/{packId}/{versionId}";
        _logger.Information("Fetching FTB version manifest: pack={PackId}, version={VersionId}", packId, versionId);

        var response = await _httpManager.GetAsync(url, cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Failed to fetch FTB version manifest {PackId}/{VersionId}: {Status}", packId, versionId, response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<FtbVersionManifest>(json, JsonOptions);
    }

    /// <summary>
    /// Import an FTB modpack by pack ID and version ID.
    /// Downloads all mod files and creates a launcher instance.
    /// </summary>
    public async Task<Instance?> ImportAsync(
        long packId,
        long versionId,
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report(("Fetching FTB pack info...", 0));

        var packInfo = await GetPackInfoAsync(packId, ct);
        if (packInfo == null)
        {
            _logger.Error("Could not fetch FTB pack info for {PackId}", packId);
            return null;
        }

        var manifest = await GetVersionManifestAsync(packId, versionId, ct);
        if (manifest == null)
        {
            _logger.Error("Could not fetch FTB version manifest for {PackId}/{VersionId}", packId, versionId);
            return null;
        }

        var instanceName = packInfo.Name ?? $"FTB Pack {packId}";
        _logger.Information("Importing FTB pack '{Name}' version {VersionId}", instanceName, versionId);

        // Build component list from targets
        var components = BuildComponents(manifest);
        if (components.Count == 0)
        {
            _logger.Error("Could not determine Minecraft version for FTB pack {PackId}", packId);
            return null;
        }

        progress?.Report(("Creating instance...", 0.05));
        var instance = await _instanceManager.CreateInstanceAsync(instanceName, components);
        if (instance == null)
        {
            _logger.Error("Failed to create instance for FTB pack {PackId}", packId);
            return null;
        }

        // Download mod files
        var modFiles = manifest.Files?.Where(f => f.Type == "mod" || f.Type == "cf-extract" || f.Type == null).ToList()
                       ?? new List<FtbFile>();

        _logger.Information("Downloading {Count} files for FTB pack {PackId}", modFiles.Count, packId);

        var modsDir = Path.Combine(instance.GameDataPath, "mods");
        Directory.CreateDirectory(modsDir);

        var configDir = Path.Combine(instance.GameDataPath, "config");
        Directory.CreateDirectory(configDir);

        int downloaded = 0;
        foreach (var file in modFiles)
        {
            if (ct.IsCancellationRequested) break;
            if (string.IsNullOrEmpty(file.Url)) continue;

            var destDir = ResolveDestinationDir(file.Path, instance.GameDataPath);
            Directory.CreateDirectory(destDir);

            var destFile = Path.Combine(destDir, file.Name ?? Path.GetFileName(file.Url));

            progress?.Report(($"Downloading {file.Name ?? "file"}...", 0.1 + (double)downloaded / modFiles.Count * 0.85));

            try
            {
                var fileResponse = await _httpManager.GetAsync(file.Url, cancellationToken: ct);
                if (fileResponse.IsSuccessStatusCode)
                {
                    var bytes = await fileResponse.Content.ReadAsByteArrayAsync(ct);
                    await File.WriteAllBytesAsync(destFile, bytes, ct);
                    _logger.Debug("Downloaded FTB file: {FileName}", file.Name);
                }
                else
                {
                    _logger.Warning("Failed to download FTB file {FileName}: {Status}", file.Name, fileResponse.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to download FTB file {FileName}", file.Name);
            }

            downloaded++;
        }

        progress?.Report(($"Imported '{instanceName}' successfully", 1.0));
        _logger.Information("FTB pack import complete: {Name} ({Downloaded}/{Total} files)", instanceName, downloaded, modFiles.Count);
        return instance;
    }

    private static List<Component> BuildComponents(FtbVersionManifest manifest)
    {
        var components = new List<Component>();

        var targets = manifest.Targets ?? new List<FtbTarget>();

        var mcTarget = targets.FirstOrDefault(t => t.Name?.Equals("minecraft", StringComparison.OrdinalIgnoreCase) == true);
        if (mcTarget == null || string.IsNullOrEmpty(mcTarget.Version)) return components;

        components.Add(new Component { Uid = "net.minecraft", Version = mcTarget.Version });

        // Mod loader (Forge, NeoForge, Fabric, etc.)
        var loaderTarget = targets.FirstOrDefault(t =>
            !t.Name.Equals("minecraft", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(t.Version));

        if (loaderTarget != null)
        {
            var uid = loaderTarget.Name?.ToLowerInvariant() switch
            {
                "forge" => "net.minecraftforge",
                "neoforge" => "net.neoforged.neoforge",
                "fabric" => "net.fabricmc.fabric-loader",
                "quilt" => "org.quiltmc.quilt-loader",
                _ => null
            };

            if (uid != null)
            {
                // Fabric/Quilt encode as "gameVer/loaderVer" in our system
                var version = (uid == "net.fabricmc.fabric-loader" || uid == "org.quiltmc.quilt-loader")
                    ? $"{mcTarget.Version}/{loaderTarget.Version}"
                    : loaderTarget.Version ?? "";

                components.Add(new Component { Uid = uid, Version = version });
            }
        }

        return components;
    }

    private static string ResolveDestinationDir(string? packPath, string instanceGameDataPath)
    {
        if (string.IsNullOrEmpty(packPath) || packPath == "/" || packPath == ".")
            return Path.Combine(instanceGameDataPath, "mods");

        // packPath may be "/mods", "config/", etc.
        var cleaned = packPath.TrimStart('/').TrimEnd('/');
        if (string.IsNullOrEmpty(cleaned)) return Path.Combine(instanceGameDataPath, "mods");

        return Path.Combine(instanceGameDataPath, cleaned.Replace('/', Path.DirectorySeparatorChar));
    }
}

// ── FTB API Models ──────────────────────────────────────────────────────────

public class FtbPackSummary
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Synopsis { get; set; } = "";
    public int Installs { get; set; }
}

public class FtbSearchResult
{
    [JsonPropertyName("packs")]
    public List<FtbSearchPackEntry>? Packs { get; set; }
}

public class FtbSearchPackEntry
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("synopsis")]
    public string? Synopsis { get; set; }
    [JsonPropertyName("installs")]
    public int Installs { get; set; }
}

public class FtbPackInfo
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("synopsis")]
    public string? Synopsis { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("versions")]
    public List<FtbPackVersion>? Versions { get; set; }
}

public class FtbPackVersion
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class FtbVersionManifest
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("targets")]
    public List<FtbTarget>? Targets { get; set; }
    [JsonPropertyName("files")]
    public List<FtbFile>? Files { get; set; }
}

public class FtbTarget
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("version")]
    public string? Version { get; set; }
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class FtbFile
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("path")]
    public string? Path { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    [JsonPropertyName("sha1")]
    public string? Sha1 { get; set; }
    [JsonPropertyName("size")]
    public long Size { get; set; }
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    [JsonPropertyName("version")]
    public string? Version { get; set; }
}
