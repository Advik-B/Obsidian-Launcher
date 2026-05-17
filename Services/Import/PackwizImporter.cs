using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services.Import;

/// <summary>
/// Imports Packwiz packs by fetching the pack.toml from a URL or local path,
/// resolving all mods from their .pw.toml index files, and downloading them.
/// </summary>
public class PackwizImporter
{
    private readonly InstanceManager _instanceManager;
    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;

    public PackwizImporter(InstanceManager instanceManager, HttpManager httpManager)
    {
        _instanceManager = instanceManager ?? throw new ArgumentNullException(nameof(instanceManager));
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _logger = LogHelper.GetLogger<PackwizImporter>();
    }

    /// <summary>
    /// Imports a Packwiz pack from a pack.toml URL.
    /// </summary>
    public async Task<Instance?> ImportFromUrlAsync(
        string packTomlUrl,
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        _logger.Information("Importing Packwiz pack from {Url}", packTomlUrl);
        progress?.Report(("Fetching pack.toml...", 0));

        var packTomlResponse = await _httpManager.GetAsync(packTomlUrl, cancellationToken: ct);
        if (!packTomlResponse.IsSuccessStatusCode)
        {
            _logger.Error("Failed to fetch pack.toml from {Url}: {Status}", packTomlUrl, packTomlResponse.StatusCode);
            return null;
        }

        var packTomlContent = await packTomlResponse.Content.ReadAsStringAsync(ct);
        var packMeta = ParsePackToml(packTomlContent);
        if (packMeta == null)
        {
            _logger.Error("Failed to parse pack.toml from {Url}", packTomlUrl);
            return null;
        }

        _logger.Information("Packwiz pack: {Name} {Version}", packMeta.Name, packMeta.Version);
        progress?.Report(($"Installing {packMeta.Name}...", 5));

        // Determine base URL (directory containing pack.toml)
        var baseUrl = packTomlUrl.Substring(0, packTomlUrl.LastIndexOf('/') + 1);

        var components = BuildComponents(packMeta);
        var instance = await _instanceManager.CreateInstanceAsync(packMeta.Name, components, cancellationToken: ct);
        if (instance == null)
        {
            _logger.Error("Failed to create instance for Packwiz pack {Name}", packMeta.Name);
            return null;
        }

        // Fetch the index file
        if (!string.IsNullOrEmpty(packMeta.IndexFile))
        {
            progress?.Report(("Fetching mod index...", 10));
            await DownloadIndexFilesAsync(baseUrl, packMeta.IndexFile, instance.InstancePath, progress, ct);
        }

        progress?.Report(("Done!", 100));
        _logger.Information("Packwiz pack import complete: {Name}", packMeta.Name);
        return instance;
    }

    private async Task DownloadIndexFilesAsync(
        string baseUrl,
        string indexFile,
        string instancePath,
        IProgress<(string Status, double Progress)>? progress,
        CancellationToken ct)
    {
        var indexUrl = baseUrl + indexFile;
        var indexResponse = await _httpManager.GetAsync(indexUrl, cancellationToken: ct);
        if (!indexResponse.IsSuccessStatusCode)
        {
            _logger.Warning("Could not fetch Packwiz index file from {Url}", indexUrl);
            return;
        }

        var indexContent = await indexResponse.Content.ReadAsStringAsync(ct);
        var modFiles = ParseIndexToml(indexContent);

        var total = modFiles.Count;
        var done = 0;

        foreach (var modFile in modFiles)
        {
            ct.ThrowIfCancellationRequested();

            var modTomlUrl = baseUrl + modFile.File;
            var modTomlResponse = await _httpManager.GetAsync(modTomlUrl, cancellationToken: ct);
            if (!modTomlResponse.IsSuccessStatusCode)
            {
                _logger.Warning("Could not fetch mod toml: {Url}", modTomlUrl);
                done++;
                continue;
            }

            var modTomlContent = await modTomlResponse.Content.ReadAsStringAsync(ct);
            var modInfo = ParseModToml(modTomlContent);

            if (modInfo?.Download?.Url != null)
            {
                var destPath = Path.Combine(instancePath, modInfo.Path?.Replace('/', Path.DirectorySeparatorChar) ?? "mods/" + modInfo.Name + ".jar");
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                if (!File.Exists(destPath))
                {
                    try
                    {
                        var (resp, _) = await _httpManager.DownloadAsync(modInfo.Download.Url, destPath, cancellationToken: ct);
                        if (!resp.IsSuccessStatusCode)
                            _logger.Warning("Failed to download packwiz mod {Name}: {Status}", modInfo.Name, resp.StatusCode);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Error downloading packwiz mod {Name}", modInfo.Name);
                    }
                }
            }

            done++;
            progress?.Report(($"Downloading mods... ({done}/{total})", 15 + done * 75.0 / total));
        }
    }

    private static PackwizPackMeta? ParsePackToml(string toml)
    {
        var meta = new PackwizPackMeta();

        foreach (var line in toml.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("name ="))
                meta.Name = ExtractTomlString(trimmed);
            else if (trimmed.StartsWith("version ="))
                meta.Version = ExtractTomlString(trimmed);
            else if (trimmed.StartsWith("file =") && meta.IndexFile == null)
                meta.IndexFile = ExtractTomlString(trimmed);
        }

        // Look for [versions] section for mc-version and loader
        bool inVersions = false;
        foreach (var line in toml.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed == "[versions]") { inVersions = true; continue; }
            if (trimmed.StartsWith("[")) { inVersions = false; continue; }
            if (!inVersions) continue;

            if (trimmed.StartsWith("minecraft ="))
                meta.MinecraftVersion = ExtractTomlString(trimmed);
            else if (trimmed.StartsWith("fabric ="))
                meta.FabricVersion = ExtractTomlString(trimmed);
            else if (trimmed.StartsWith("quilt ="))
                meta.QuiltVersion = ExtractTomlString(trimmed);
            else if (trimmed.StartsWith("forge ="))
                meta.ForgeVersion = ExtractTomlString(trimmed);
            else if (trimmed.StartsWith("neoforge ="))
                meta.NeoForgeVersion = ExtractTomlString(trimmed);
        }

        return string.IsNullOrEmpty(meta.Name) ? null : meta;
    }

    private static List<PackwizIndexEntry> ParseIndexToml(string toml)
    {
        var entries = new List<PackwizIndexEntry>();
        PackwizIndexEntry? current = null;

        foreach (var line in toml.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("[[files]]"))
            {
                current = new PackwizIndexEntry();
                entries.Add(current);
            }
            else if (current != null)
            {
                if (trimmed.StartsWith("file ="))
                    current.File = ExtractTomlString(trimmed);
            }
        }

        return entries;
    }

    private static PackwizModInfo? ParseModToml(string toml)
    {
        var mod = new PackwizModInfo();
        bool inDownload = false;

        foreach (var line in toml.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed == "[download]") { inDownload = true; continue; }
            if (trimmed.StartsWith("[")) { inDownload = false; continue; }

            if (trimmed.StartsWith("name ="))
                mod.Name = ExtractTomlString(trimmed);
            else if (trimmed.StartsWith("filename ="))
                mod.Path = ExtractTomlString(trimmed);

            if (inDownload)
            {
                mod.Download ??= new PackwizDownload();
                if (trimmed.StartsWith("url ="))
                    mod.Download.Url = ExtractTomlString(trimmed);
                else if (trimmed.StartsWith("hash ="))
                    mod.Download.Hash = ExtractTomlString(trimmed);
            }
        }

        return mod;
    }

    private static string ExtractTomlString(string line)
    {
        var eqIdx = line.IndexOf('=');
        if (eqIdx < 0) return string.Empty;
        var val = line.Substring(eqIdx + 1).Trim();
        return val.Trim('"', '\'');
    }

    private static List<Component> BuildComponents(PackwizPackMeta meta)
    {
        var components = new List<Component>();

        if (!string.IsNullOrEmpty(meta.MinecraftVersion))
            components.Add(new Component { Uid = "net.minecraft", Version = meta.MinecraftVersion, IsEnabled = true, IsImportant = true });

        if (!string.IsNullOrEmpty(meta.FabricVersion))
            components.Add(new Component { Uid = "net.fabricmc.fabric-loader", Version = meta.FabricVersion, IsEnabled = true });

        if (!string.IsNullOrEmpty(meta.QuiltVersion))
            components.Add(new Component { Uid = "org.quiltmc.quilt-loader", Version = meta.QuiltVersion, IsEnabled = true });

        if (!string.IsNullOrEmpty(meta.ForgeVersion))
            components.Add(new Component { Uid = "net.minecraftforge", Version = meta.ForgeVersion, IsEnabled = true });

        if (!string.IsNullOrEmpty(meta.NeoForgeVersion))
            components.Add(new Component { Uid = "net.neoforged.neoforge", Version = meta.NeoForgeVersion, IsEnabled = true });

        return components;
    }

    private class PackwizPackMeta
    {
        public string Name { get; set; } = string.Empty;
        public string? Version { get; set; }
        public string? IndexFile { get; set; }
        public string? MinecraftVersion { get; set; }
        public string? FabricVersion { get; set; }
        public string? QuiltVersion { get; set; }
        public string? ForgeVersion { get; set; }
        public string? NeoForgeVersion { get; set; }
    }

    private class PackwizIndexEntry
    {
        public string File { get; set; } = string.Empty;
    }

    private class PackwizModInfo
    {
        public string Name { get; set; } = string.Empty;
        public string? Path { get; set; }
        public PackwizDownload? Download { get; set; }
    }

    private class PackwizDownload
    {
        public string? Url { get; set; }
        public string? Hash { get; set; }
        public string HashFormat { get; set; } = "sha256";
    }
}
