using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Models.Forge;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services.ModLoaders;

/// <summary>
/// Downloads and processes the NeoForge mod loader installer.
/// NeoForge is the community successor to Forge starting from Minecraft 1.20.1.
/// </summary>
public class NeoForgeInstaller
{
    private const string NeoForgeMaven = "https://maven.neoforged.net/releases";
    private const string NeoForgeGroup = "net/neoforged/neoforge";

    private readonly HttpManager _httpManager;
    private readonly LauncherConfig _config;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public NeoForgeInstaller(HttpManager httpManager, LauncherConfig config)
    {
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = LogHelper.GetLogger<NeoForgeInstaller>();
    }

    /// <summary>
    /// Returns available NeoForge versions by querying the Maven metadata.
    /// </summary>
    public async Task<List<string>> GetNeoForgeVersionsAsync(CancellationToken ct = default)
    {
        var url = $"{NeoForgeMaven}/{NeoForgeGroup}/maven-metadata.xml";
        _logger.Information("Fetching NeoForge version list");

        var response = await _httpManager.GetAsync(url, cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Failed to fetch NeoForge Maven metadata: {Status}", response.StatusCode);
            return new List<string>();
        }

        var xml = await response.Content.ReadAsStringAsync(ct);
        var versions = new List<string>();

        var startTag = "<version>";
        var endTag = "</version>";
        var searchFrom = 0;
        while (true)
        {
            var start = xml.IndexOf(startTag, searchFrom, StringComparison.Ordinal);
            if (start < 0) break;
            var end = xml.IndexOf(endTag, start, StringComparison.Ordinal);
            if (end < 0) break;

            var ver = xml.Substring(start + startTag.Length, end - start - startTag.Length).Trim();
            if (!string.IsNullOrEmpty(ver))
                versions.Add(ver);

            searchFrom = end + endTag.Length;
        }

        versions.Reverse();
        _logger.Information("Found {Count} NeoForge versions", versions.Count);
        return versions;
    }

    /// <summary>
    /// Downloads the NeoForge installer, extracts the version profile, and returns
    /// the MinecraftVersion ready to be merged into the launch profile.
    /// </summary>
    public async Task<MinecraftVersion?> GetProfileAsync(string neoForgeVersion, CancellationToken ct = default)
    {
        var installerUrl = $"{NeoForgeMaven}/{NeoForgeGroup}/{neoForgeVersion}/neoforge-{neoForgeVersion}-installer.jar";
        var installerPath = Path.Combine(_config.LibrariesDir, "neoforge-installers",
            $"neoforge-{neoForgeVersion}-installer.jar");

        Directory.CreateDirectory(Path.GetDirectoryName(installerPath)!);

        if (!File.Exists(installerPath))
        {
            _logger.Information("Downloading NeoForge installer: {Url}", installerUrl);
            var (resp, _) = await _httpManager.DownloadAsync(installerUrl, installerPath, cancellationToken: ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.Error("Failed to download NeoForge installer {Version}: {Status}", neoForgeVersion, resp.StatusCode);
                return null;
            }
        }

        return await ProcessInstallerAsync(installerPath, neoForgeVersion, ct);
    }

    private async Task<MinecraftVersion?> ProcessInstallerAsync(string installerPath, string neoForgeVersion, CancellationToken ct)
    {
        try
        {
            using var archive = ZipFile.OpenRead(installerPath);

            // NeoForge uses the new Forge installer format
            var versionJsonEntry = archive.Entries.FirstOrDefault(e => e.FullName == "version.json");
            if (versionJsonEntry != null)
            {
                using var stream = versionJsonEntry.Open();
                var json = await new StreamReader(stream).ReadToEndAsync(ct);
                var profile = JsonSerializer.Deserialize<MinecraftVersion>(json, JsonOptions);

                if (profile == null)
                {
                    _logger.Error("Failed to parse version.json from NeoForge installer {Version}", neoForgeVersion);
                    return null;
                }

                // Process install_profile.json for installer libraries
                var installProfileEntry = archive.Entries.FirstOrDefault(e => e.FullName == "install_profile.json");
                if (installProfileEntry != null)
                {
                    using var ipStream = installProfileEntry.Open();
                    var ipJson = await new StreamReader(ipStream).ReadToEndAsync(ct);
                    var installProfile = JsonSerializer.Deserialize<ForgeInstallProfile>(ipJson, JsonOptions);

                    if (installProfile?.Libraries != null)
                        await DownloadInstallerLibrariesAsync(installProfile.Libraries, ct);
                }

                // Extract bundled maven libraries
                await ExtractBundledLibrariesAsync(archive, ct);

                _logger.Information("NeoForge {Version} profile ready: mainClass={MainClass}", neoForgeVersion, profile.MainClass);
                return profile;
            }

            _logger.Error("NeoForge installer {Version} does not contain version.json", neoForgeVersion);
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing NeoForge installer {Version}", neoForgeVersion);
            return null;
        }
    }

    private async Task DownloadInstallerLibrariesAsync(List<ForgeLibrary> libraries, CancellationToken ct)
    {
        foreach (var lib in libraries)
        {
            if (lib.Downloads?.Artifact == null) continue;
            var artifact = lib.Downloads.Artifact;
            if (string.IsNullOrEmpty(artifact.Url) || string.IsNullOrEmpty(artifact.Path))
                continue;

            var localPath = Path.Combine(_config.LibrariesDir, artifact.Path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(localPath)) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            _logger.Debug("Downloading NeoForge library: {Path}", artifact.Path);
            var (resp, _) = await _httpManager.DownloadAsync(artifact.Url, localPath, cancellationToken: ct);
            if (!resp.IsSuccessStatusCode)
                _logger.Warning("Failed to download NeoForge library {Path}: {Status}", artifact.Path, resp.StatusCode);
        }
    }

    private async Task ExtractBundledLibrariesAsync(ZipArchive archive, CancellationToken ct)
    {
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith("maven/", StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.FullName.EndsWith("/")) continue;

            var relPath = entry.FullName.Substring("maven/".Length).Replace('/', Path.DirectorySeparatorChar);
            var destPath = Path.Combine(_config.LibrariesDir, relPath);

            if (File.Exists(destPath)) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            ct.ThrowIfCancellationRequested();

            await using var entryStream = entry.Open();
            await using var fs = File.Create(destPath);
            await entryStream.CopyToAsync(fs, ct);
        }
    }
}
