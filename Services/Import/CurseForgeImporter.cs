using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
/// Imports CurseForge modpack ZIPs (manifest.json format).
/// Note: CurseForge mod files require either the CurseForge API or Overwolf App for download.
/// This importer handles the manifest parsing and structure creation; mod downloads
/// use the CurseForge CDN where available.
/// </summary>
public class CurseForgeImporter
{
    private const string CfCdnBase = "https://edge.forgecdn.net/files";

    private readonly InstanceManager _instanceManager;
    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CurseForgeImporter(InstanceManager instanceManager, HttpManager httpManager)
    {
        _instanceManager = instanceManager ?? throw new ArgumentNullException(nameof(instanceManager));
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _logger = LogHelper.GetLogger<CurseForgeImporter>();
    }

    /// <summary>
    /// Imports a CurseForge modpack ZIP file.
    /// </summary>
    public async Task<Instance?> ImportAsync(
        string zipPath,
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(zipPath))
        {
            _logger.Error("CurseForge zip not found: {Path}", zipPath);
            return null;
        }

        _logger.Information("Importing CurseForge modpack from {Path}", zipPath);
        progress?.Report(("Reading modpack...", 0));

        using var archive = ZipFile.OpenRead(zipPath);

        var manifestEntry = archive.Entries.FirstOrDefault(e =>
            e.Name == "manifest.json" || e.FullName.EndsWith("/manifest.json"));
        if (manifestEntry == null)
        {
            _logger.Error("manifest.json not found in {Path}", zipPath);
            return null;
        }

        CurseForgeManifest? manifest;
        using (var stream = manifestEntry.Open())
        {
            manifest = await JsonSerializer.DeserializeAsync<CurseForgeManifest>(stream, JsonOptions, ct);
        }

        if (manifest == null)
        {
            _logger.Error("Failed to parse manifest.json");
            return null;
        }

        _logger.Information("CurseForge modpack: {Name} {Version}", manifest.Name, manifest.Version);
        progress?.Report(($"Installing {manifest.Name}...", 5));

        var components = BuildComponents(manifest);
        var instance = await _instanceManager.CreateInstanceAsync(manifest.Name, components, cancellationToken: ct);
        if (instance == null)
        {
            _logger.Error("Failed to create instance for CurseForge modpack {Name}", manifest.Name);
            return null;
        }

        var instancePath = instance.InstancePath;
        var modsDir = Path.Combine(instancePath, "mods");
        Directory.CreateDirectory(modsDir);

        // Download mods
        var totalFiles = manifest.Files?.Count ?? 0;
        var completed = 0;

        if (manifest.Files != null)
        {
            foreach (var file in manifest.Files)
            {
                ct.ThrowIfCancellationRequested();

                if (!file.Required)
                {
                    completed++;
                    continue;
                }

                var downloadUrl = BuildCdnUrl(file.ProjectId, file.FileId);
                var tempPath = Path.Combine(modsDir, $"{file.ProjectId}_{file.FileId}.jar");

                try
                {
                    _logger.Debug("Downloading CurseForge file {ProjectId}/{FileId}", file.ProjectId, file.FileId);
                    var (resp, _) = await _httpManager.DownloadAsync(downloadUrl, tempPath, cancellationToken: ct);
                    if (!resp.IsSuccessStatusCode)
                    {
                        _logger.Warning("Failed to download CurseForge file {ProjectId}/{FileId}: {Status}",
                            file.ProjectId, file.FileId, resp.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Error downloading CurseForge file {ProjectId}/{FileId}", file.ProjectId, file.FileId);
                }

                completed++;
                progress?.Report(($"Downloading mods... ({completed}/{totalFiles})", 10 + (completed * 80.0 / totalFiles)));
            }
        }

        // Extract overrides
        progress?.Report(("Applying overrides...", 92));
        var overridesPrefix = (manifest.Overrides ?? "overrides").TrimEnd('/') + "/";
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (!entry.FullName.StartsWith(overridesPrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (entry.FullName.Length == overridesPrefix.Length)
                continue;

            var relPath = entry.FullName.Substring(overridesPrefix.Length).Replace('/', Path.DirectorySeparatorChar);
            var destPath = Path.Combine(instancePath, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            await using var entryStream = entry.Open();
            await using var fs = File.Create(destPath);
            await entryStream.CopyToAsync(fs, ct);
        }

        progress?.Report(("Done!", 100));
        _logger.Information("CurseForge modpack import complete: {Name}", manifest.Name);
        return instance;
    }

    private static List<Component> BuildComponents(CurseForgeManifest manifest)
    {
        var components = new List<Component>();

        components.Add(new Component
        {
            Uid = "net.minecraft",
            Version = manifest.Minecraft?.Version ?? "1.20.1",
            IsEnabled = true,
            IsImportant = true
        });

        if (manifest.Minecraft?.ModLoaders != null)
        {
            foreach (var loader in manifest.Minecraft.ModLoaders.Where(l => l.Primary))
            {
                var loaderStr = loader.Id ?? "";
                if (loaderStr.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
                {
                    components.Add(new Component
                    {
                        Uid = "net.minecraftforge",
                        Version = loaderStr.Substring("forge-".Length),
                        IsEnabled = true
                    });
                }
                else if (loaderStr.StartsWith("neoforge-", StringComparison.OrdinalIgnoreCase))
                {
                    components.Add(new Component
                    {
                        Uid = "net.neoforged.neoforge",
                        Version = loaderStr.Substring("neoforge-".Length),
                        IsEnabled = true
                    });
                }
                else if (loaderStr.StartsWith("fabric-", StringComparison.OrdinalIgnoreCase))
                {
                    components.Add(new Component
                    {
                        Uid = "net.fabricmc.fabric-loader",
                        Version = loaderStr.Substring("fabric-".Length),
                        IsEnabled = true
                    });
                }
            }
        }

        return components;
    }

    private static string BuildCdnUrl(int projectId, int fileId)
    {
        // CurseForge CDN URL pattern: /files/{fileId/1000}/{fileId%1000}/{filename}
        // Without filename we can try a known-working pattern
        var part1 = fileId / 1000;
        var part2 = fileId % 1000;
        return $"{CfCdnBase}/{part1:D4}/{part2:D3}/{projectId}-{fileId}.jar";
    }
}

#region CurseForge Manifest Models

public class CurseForgeManifest
{
    [JsonPropertyName("minecraft")]
    public CurseForgeMinecraft? Minecraft { get; set; }

    [JsonPropertyName("manifestType")]
    public string? ManifestType { get; set; }

    [JsonPropertyName("manifestVersion")]
    public int ManifestVersion { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("files")]
    public List<CurseForgeFile>? Files { get; set; }

    [JsonPropertyName("overrides")]
    public string? Overrides { get; set; }
}

public class CurseForgeMinecraft
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("modLoaders")]
    public List<CurseForgeModLoader>? ModLoaders { get; set; }
}

public class CurseForgeModLoader
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }
}

public class CurseForgeFile
{
    [JsonPropertyName("projectID")]
    public int ProjectId { get; set; }

    [JsonPropertyName("fileID")]
    public int FileId { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; } = true;
}

#endregion
