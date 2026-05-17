using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Models.Modrinth;
using ObsidianLauncher.Services.Modrinth;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services.Import;

/// <summary>
/// Imports Modrinth modpacks (.mrpack files).
/// </summary>
public class ModrinthImporter
{
    private readonly InstanceManager _instanceManager;
    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ModrinthImporter(InstanceManager instanceManager, HttpManager httpManager)
    {
        _instanceManager = instanceManager ?? throw new ArgumentNullException(nameof(instanceManager));
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _logger = LogHelper.GetLogger<ModrinthImporter>();
    }

    /// <summary>
    /// Imports a .mrpack file into a new instance.
    /// </summary>
    public async Task<Instance?> ImportAsync(
        string mrpackPath,
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(mrpackPath))
        {
            _logger.Error("mrpack file not found: {Path}", mrpackPath);
            return null;
        }

        _logger.Information("Importing Modrinth modpack from {Path}", mrpackPath);
        progress?.Report(("Reading modpack...", 0));

        using var archive = ZipFile.OpenRead(mrpackPath);

        var indexEntry = archive.Entries.FirstOrDefault(e => e.FullName == "modrinth.index.json");
        if (indexEntry == null)
        {
            _logger.Error("modrinth.index.json not found in {Path}", mrpackPath);
            return null;
        }

        ModrinthIndex? index;
        using (var stream = indexEntry.Open())
        {
            index = await JsonSerializer.DeserializeAsync<ModrinthIndex>(stream, JsonOptions, ct);
        }

        if (index == null)
        {
            _logger.Error("Failed to parse modrinth.index.json from {Path}", mrpackPath);
            return null;
        }

        _logger.Information("Modpack: {Name} version {VersionId}", index.Name, index.VersionId);
        progress?.Report(($"Installing {index.Name}...", 5));

        // Build components from dependencies
        var components = BuildComponents(index);

        // Create the instance
        var instance = await _instanceManager.CreateInstanceAsync(
            index.Name,
            components,
            cancellationToken: ct);

        if (instance == null)
        {
            _logger.Error("Failed to create instance for modpack {Name}", index.Name);
            return null;
        }

        var instancePath = instance.InstancePath;
        Directory.CreateDirectory(instancePath);

        // Download all mod files listed in the index
        var totalFiles = index.Files.Count;
        var completed = 0;

        _logger.Information("Downloading {Count} files for modpack {Name}", totalFiles, index.Name);

        foreach (var file in index.Files)
        {
            ct.ThrowIfCancellationRequested();

            // Skip server-only files
            if (file.Env?.Client == "unsupported")
            {
                _logger.Debug("Skipping server-only file: {Path}", file.Path);
                completed++;
                continue;
            }

            var destPath = Path.Combine(instancePath, file.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            // Check if already exists with correct hash
            if (File.Exists(destPath) && file.Hashes.Sha1 != null)
            {
                var existing = await CryptoUtils.CalculateFileSHA1Async(destPath, ct);
                if (string.Equals(existing, file.Hashes.Sha1, StringComparison.OrdinalIgnoreCase))
                {
                    completed++;
                    progress?.Report(($"Checking files... ({completed}/{totalFiles})", 10 + (completed * 80.0 / totalFiles)));
                    continue;
                }
            }

            // Try each download URL
            bool downloaded = false;
            foreach (var dlUrl in file.Downloads)
            {
                try
                {
                    _logger.Debug("Downloading {Path} from {Url}", file.Path, dlUrl);
                    var (resp, _) = await _httpManager.DownloadAsync(dlUrl, destPath, cancellationToken: ct);
                    if (resp.IsSuccessStatusCode)
                    {
                        downloaded = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to download {Path} from {Url}", file.Path, dlUrl);
                }
            }

            if (!downloaded)
                _logger.Warning("Could not download file: {Path}", file.Path);

            completed++;
            progress?.Report(($"Downloading files... ({completed}/{totalFiles})", 10 + (completed * 80.0 / totalFiles)));
        }

        // Extract overrides
        progress?.Report(("Applying overrides...", 92));
        await ExtractOverridesAsync(archive, instancePath, "overrides", ct);
        await ExtractOverridesAsync(archive, instancePath, "client-overrides", ct);

        progress?.Report(("Done!", 100));
        _logger.Information("Modrinth modpack import complete: {Name}", index.Name);
        return instance;
    }

    private static List<Component> BuildComponents(ModrinthIndex index)
    {
        var components = new List<Component>();

        if (index.Dependencies.TryGetValue("minecraft", out var mcVersion))
        {
            components.Add(new Component
            {
                Uid = "net.minecraft",
                Version = mcVersion,
                IsEnabled = true,
                IsImportant = true
            });
        }

        if (index.Dependencies.TryGetValue("fabric-loader", out var fabricVersion))
        {
            components.Add(new Component
            {
                Uid = "net.fabricmc.fabric-loader",
                Version = fabricVersion,
                IsEnabled = true
            });
        }

        if (index.Dependencies.TryGetValue("quilt-loader", out var quiltVersion))
        {
            components.Add(new Component
            {
                Uid = "org.quiltmc.quilt-loader",
                Version = quiltVersion,
                IsEnabled = true
            });
        }

        if (index.Dependencies.TryGetValue("forge", out var forgeVersion))
        {
            components.Add(new Component
            {
                Uid = "net.minecraftforge",
                Version = forgeVersion,
                IsEnabled = true
            });
        }

        if (index.Dependencies.TryGetValue("neoforge", out var neoForgeVersion))
        {
            components.Add(new Component
            {
                Uid = "net.neoforged.neoforge",
                Version = neoForgeVersion,
                IsEnabled = true
            });
        }

        return components;
    }

    private async Task ExtractOverridesAsync(ZipArchive archive, string instancePath, string folderName, CancellationToken ct)
    {
        var prefix = folderName + "/";
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (!entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (entry.FullName.Length == prefix.Length)
                continue; // Skip directory entry itself

            var relPath = entry.FullName.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
            var destPath = Path.Combine(instancePath, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            await using var entryStream = entry.Open();
            await using var fileStream = File.Create(destPath);
            await entryStream.CopyToAsync(fileStream, ct);
        }
    }
}
