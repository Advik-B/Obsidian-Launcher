using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Models.Modrinth;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services.Modrinth;

/// <summary>
/// High-level service for managing mods in an instance via Modrinth.
/// Handles browsing, downloading, installing, enabling, disabling, and removing mods.
/// </summary>
public class ModManager
{
    private readonly ModrinthClient _client;
    private readonly ILogger _logger;

    public ModManager(ModrinthClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = LogHelper.GetLogger<ModManager>();
    }

    /// <summary>
    /// Returns the mods directory for an instance.
    /// </summary>
    public static string GetModsDir(Instance instance) =>
        Path.Combine(instance.InstancePath, "mods");

    /// <summary>
    /// Lists all mod files (.jar, .disabled) in the instance's mods directory.
    /// </summary>
    public List<InstalledMod> GetInstalledMods(Instance instance)
    {
        var modsDir = GetModsDir(instance);
        if (!Directory.Exists(modsDir))
            return new List<InstalledMod>();

        var mods = new List<InstalledMod>();
        foreach (var file in Directory.EnumerateFiles(modsDir))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".jar" || ext == ".disabled")
            {
                mods.Add(new InstalledMod
                {
                    FileName = Path.GetFileName(file),
                    FilePath = file,
                    IsEnabled = ext == ".jar"
                });
            }
        }

        _logger.Debug("Found {Count} installed mods in {InstanceName}", mods.Count, instance.Name);
        return mods;
    }

    /// <summary>
    /// Downloads and installs a Modrinth mod version into an instance.
    /// Returns the installed file path on success, null on failure.
    /// </summary>
    public async Task<string?> InstallModAsync(
        Instance instance,
        ModrinthVersion version,
        IProgress<float>? progress = null,
        CancellationToken ct = default)
    {
        var modsDir = GetModsDir(instance);
        Directory.CreateDirectory(modsDir);

        var downloaded = await _client.DownloadVersionFileAsync(version, modsDir, progress, ct);
        if (downloaded == null)
        {
            _logger.Error("Failed to download mod version {VersionId}", version.Id);
            return null;
        }

        // Save metadata alongside the mod
        await SaveModMetadataAsync(modsDir, version, downloaded, ct);

        _logger.Information("Installed mod {Name} ({VersionId}) into {InstanceName}",
            version.Name, version.Id, instance.Name);
        return downloaded;
    }

    /// <summary>
    /// Removes a mod from the instance.
    /// </summary>
    public bool RemoveMod(Instance instance, string fileName)
    {
        var modsDir = GetModsDir(instance);
        var jarPath = Path.Combine(modsDir, fileName);
        var disabledPath = jarPath + ".disabled";
        var metaPath = jarPath + ".meta.json";

        bool removed = false;
        if (File.Exists(jarPath))
        {
            File.Delete(jarPath);
            removed = true;
        }
        else if (File.Exists(disabledPath))
        {
            File.Delete(disabledPath);
            removed = true;
        }

        if (File.Exists(metaPath))
            File.Delete(metaPath);

        if (removed)
            _logger.Information("Removed mod {FileName} from {InstanceName}", fileName, instance.Name);

        return removed;
    }

    /// <summary>
    /// Toggles a mod enabled/disabled by renaming between .jar and .disabled.
    /// </summary>
    public bool ToggleMod(Instance instance, string fileName)
    {
        var modsDir = GetModsDir(instance);
        var path = Path.Combine(modsDir, fileName);

        if (File.Exists(path) && path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
        {
            File.Move(path, path + ".disabled");
            return true;
        }

        var disabledPath = path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
            ? path
            : path + ".disabled";

        if (File.Exists(disabledPath))
        {
            var enabledPath = disabledPath.Substring(0, disabledPath.Length - ".disabled".Length);
            File.Move(disabledPath, enabledPath);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Searches Modrinth for mods compatible with the given Minecraft version and loader.
    /// </summary>
    public Task<ModrinthSearchResult?> SearchModsAsync(
        string query,
        string? gameVersion = null,
        string? loader = null,
        int limit = 20,
        int offset = 0,
        CancellationToken ct = default)
    {
        return _client.SearchAsync(query, "mod", gameVersion, loader, limit, offset, ct);
    }

    /// <summary>
    /// Searches Modrinth for modpacks compatible with the given Minecraft version and loader.
    /// </summary>
    public Task<ModrinthSearchResult?> SearchModpacksAsync(
        string query,
        string? gameVersion = null,
        string? loader = null,
        int limit = 20,
        int offset = 0,
        CancellationToken ct = default)
    {
        return _client.SearchAsync(query, "modpack", gameVersion, loader, limit, offset, ct);
    }

    /// <summary>
    /// Gets all versions of a mod compatible with the given Minecraft version and loader.
    /// </summary>
    public Task<List<ModrinthVersion>?> GetCompatibleVersionsAsync(
        string projectIdOrSlug,
        string gameVersion,
        string? loader,
        CancellationToken ct = default)
    {
        return _client.GetProjectVersionsAsync(projectIdOrSlug, gameVersion, loader, ct);
    }

    private async Task SaveModMetadataAsync(string modsDir, ModrinthVersion version, string jarPath, CancellationToken ct)
    {
        try
        {
            var metaPath = jarPath + ".meta.json";
            var meta = new ModMetadata
            {
                ProjectId = version.ProjectId,
                VersionId = version.Id,
                VersionName = version.Name,
                GameVersions = version.GameVersions,
                Loaders = version.Loaders,
                InstalledAt = DateTime.UtcNow.ToString("O")
            };

            var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metaPath, json, ct);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to save mod metadata for {VersionId}", version.Id);
        }
    }
}

public class InstalledMod
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public ModMetadata? Metadata { get; set; }
}

public class ModMetadata
{
    public string? ProjectId { get; set; }
    public string? VersionId { get; set; }
    public string? VersionName { get; set; }
    public List<string>? GameVersions { get; set; }
    public List<string>? Loaders { get; set; }
    public string? InstalledAt { get; set; }
}
