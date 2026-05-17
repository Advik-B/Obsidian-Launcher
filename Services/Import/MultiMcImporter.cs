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

public class MultiMcImporter
{
    private readonly InstanceManager _instanceManager;
    private readonly ILogger _logger;

    public MultiMcImporter(InstanceManager instanceManager)
    {
        _instanceManager = instanceManager ?? throw new ArgumentNullException(nameof(instanceManager));
        _logger = LogHelper.GetLogger<MultiMcImporter>();
    }

    public async Task<Instance?> ImportAsync(string zipPath, CancellationToken ct = default)
    {
        if (!File.Exists(zipPath))
        {
            _logger.Error("Import zip not found: {ZipPath}", zipPath);
            return null;
        }

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);

            var mmcPackEntry = archive.Entries.FirstOrDefault(e =>
                e.Name == "mmc-pack.json" || e.FullName.EndsWith("/mmc-pack.json"));
            if (mmcPackEntry == null)
            {
                _logger.Error("mmc-pack.json not found in {ZipPath}", zipPath);
                return null;
            }

            using var mmcPackStream = mmcPackEntry.Open();
            var mmcPack = await JsonSerializer.DeserializeAsync<MmcPack>(mmcPackStream, cancellationToken: ct);
            if (mmcPack == null)
            {
                _logger.Error("Failed to parse mmc-pack.json");
                return null;
            }

            var instanceName = Path.GetFileNameWithoutExtension(zipPath);
            var cfgEntry = archive.Entries.FirstOrDefault(e => e.Name == "instance.cfg");
            if (cfgEntry != null)
            {
                using var cfgStream = cfgEntry.Open();
                using var reader = new StreamReader(cfgStream);
                var cfgText = await reader.ReadToEndAsync(ct);
                foreach (var line in cfgText.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("name=", StringComparison.OrdinalIgnoreCase))
                    {
                        instanceName = trimmed.Substring(5).Trim();
                        break;
                    }
                }
            }

            var components = BuildComponents(mmcPack);
            if (components.Count == 0)
            {
                _logger.Warning("No usable components found in mmc-pack.json for {ZipPath}", zipPath);
                return null;
            }

            var instance = await _instanceManager.CreateInstanceAsync(instanceName, components, null, null, ct);
            if (instance == null)
            {
                _logger.Error("Failed to create instance '{Name}' during MultiMC import", instanceName);
                return null;
            }

            var minecraftPrefix = FindMinecraftPrefix(archive);
            if (!string.IsNullOrEmpty(minecraftPrefix))
                ExtractMinecraftFolder(archive, minecraftPrefix, instance.InstancePath);

            _logger.Information("MultiMC import completed: '{InstanceName}'", instanceName);
            return instance;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to import MultiMC zip: {ZipPath}", zipPath);
            return null;
        }
    }

    private List<Component> BuildComponents(MmcPack mmcPack)
    {
        var result = new List<Component>();
        foreach (var comp in mmcPack.Components ?? Enumerable.Empty<MmcComponent>())
        {
            if (string.IsNullOrWhiteSpace(comp.Uid) || string.IsNullOrWhiteSpace(comp.Version))
                continue;

            var uid = comp.Uid switch
            {
                "net.minecraft"               => "net.minecraft",
                "net.fabricmc.fabric-loader"  => "net.fabricmc.fabric-loader",
                "org.quiltmc.quilt-loader"    => "org.quiltmc.quilt-loader",
                "net.minecraftforge"          => "net.minecraftforge",
                "net.neoforged.neoforge"      => "net.neoforged.neoforge",
                _                             => null
            };

            if (uid != null)
                result.Add(new Component { Uid = uid, Version = comp.Version });
        }
        return result;
    }

    private string FindMinecraftPrefix(ZipArchive archive)
    {
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith(".minecraft/", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.Contains("/.minecraft/"))
                return entry.FullName.Substring(0, entry.FullName.LastIndexOf(".minecraft/", StringComparison.OrdinalIgnoreCase) + ".minecraft/".Length);
        }
        return "";
    }

    private void ExtractMinecraftFolder(ZipArchive archive, string prefix, string destPath)
    {
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.FullName.EndsWith("/")) continue;

            var relativePath = entry.FullName.Substring(prefix.Length);
            var destFile = Path.Combine(destPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            entry.ExtractToFile(destFile, overwrite: true);
        }
    }

    private class MmcPack
    {
        [JsonPropertyName("components")]
        public List<MmcComponent>? Components { get; set; }
    }

    private class MmcComponent
    {
        [JsonPropertyName("uid")]
        public string? Uid { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }
}
