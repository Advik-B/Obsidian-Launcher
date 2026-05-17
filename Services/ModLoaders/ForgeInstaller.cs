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
/// Downloads and processes the Forge mod loader installer JAR.
/// Handles both the legacy (1.12.2 and below) and modern (1.13+) Forge formats.
/// </summary>
public class ForgeInstaller
{
    private const string ForgeMaven = "https://maven.minecraftforge.net";
    private const string ForgeMavenGroup = "net/minecraftforge/forge";

    private readonly HttpManager _httpManager;
    private readonly LauncherConfig _config;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ForgeInstaller(HttpManager httpManager, LauncherConfig config)
    {
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = LogHelper.GetLogger<ForgeInstaller>();
    }

    /// <summary>
    /// Returns available Forge versions for the given Minecraft version
    /// by parsing the Forge Maven metadata XML.
    /// </summary>
    public async Task<List<string>> GetForgeVersionsAsync(string mcVersion, CancellationToken ct = default)
    {
        var url = $"{ForgeMaven}/{ForgeMavenGroup}/maven-metadata.xml";
        _logger.Information("Fetching Forge version list from Maven metadata");

        var response = await _httpManager.GetAsync(url, cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Failed to fetch Forge Maven metadata: {Status}", response.StatusCode);
            return new List<string>();
        }

        var xml = await response.Content.ReadAsStringAsync(ct);
        var versions = new List<string>();

        // Simple XML parsing to extract <version> elements
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
            // Filter by Minecraft version prefix (e.g. "1.20.1-" matches "1.20.1-47.2.0")
            if (ver.StartsWith(mcVersion + "-", StringComparison.OrdinalIgnoreCase))
                versions.Add(ver);

            searchFrom = end + endTag.Length;
        }

        versions.Reverse(); // Newest first
        _logger.Information("Found {Count} Forge versions for MC {McVersion}", versions.Count, mcVersion);
        return versions;
    }

    /// <summary>
    /// Downloads the Forge installer JAR, extracts version.json (new format) or
    /// install_profile.json (old format), and returns the resulting MinecraftVersion profile
    /// that can be merged into the launch profile.
    /// </summary>
    public async Task<MinecraftVersion?> GetProfileAsync(
        string mcVersion,
        string forgeVersion,
        CancellationToken ct = default)
    {
        // forgeVersion may be the full "1.20.1-47.2.0" or just "47.2.0"
        var fullVersion = forgeVersion.Contains('-') ? forgeVersion : $"{mcVersion}-{forgeVersion}";

        var installerUrl = $"{ForgeMaven}/{ForgeMavenGroup}/{fullVersion}/forge-{fullVersion}-installer.jar";
        var installerPath = Path.Combine(_config.LibrariesDir, "forge-installers", $"forge-{fullVersion}-installer.jar");

        Directory.CreateDirectory(Path.GetDirectoryName(installerPath)!);

        // Download if not cached
        if (!File.Exists(installerPath))
        {
            _logger.Information("Downloading Forge installer: {Url}", installerUrl);
            var (resp, _) = await _httpManager.DownloadAsync(installerUrl, installerPath, cancellationToken: ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.Error("Failed to download Forge installer {FullVersion}: {Status}", fullVersion, resp.StatusCode);
                return null;
            }
        }

        return await ProcessInstallerAsync(installerPath, mcVersion, fullVersion, ct);
    }

    private async Task<MinecraftVersion?> ProcessInstallerAsync(
        string installerPath,
        string mcVersion,
        string fullVersion,
        CancellationToken ct)
    {
        try
        {
            using var archive = ZipFile.OpenRead(installerPath);

            // Try new format first (version.json present)
            var versionJsonEntry = archive.Entries.FirstOrDefault(e => e.FullName == "version.json");
            if (versionJsonEntry != null)
            {
                return await ProcessNewFormatAsync(archive, versionJsonEntry, mcVersion, fullVersion, ct);
            }

            // Try old format (install_profile.json only)
            var profileEntry = archive.Entries.FirstOrDefault(e => e.FullName == "install_profile.json");
            if (profileEntry != null)
            {
                return await ProcessOldFormatAsync(archive, profileEntry, fullVersion, ct);
            }

            _logger.Error("Forge installer {FullVersion} contains neither version.json nor install_profile.json", fullVersion);
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing Forge installer {FullVersion}", fullVersion);
            return null;
        }
    }

    private async Task<MinecraftVersion?> ProcessNewFormatAsync(
        ZipArchive archive,
        ZipArchiveEntry versionJsonEntry,
        string mcVersion,
        string fullVersion,
        CancellationToken ct)
    {
        _logger.Information("Processing Forge {FullVersion} (new format)", fullVersion);

        using var stream = versionJsonEntry.Open();
        var json = await new StreamReader(stream).ReadToEndAsync(ct);
        var profile = JsonSerializer.Deserialize<MinecraftVersion>(json, JsonOptions);

        if (profile == null)
        {
            _logger.Error("Failed to parse version.json from Forge installer {FullVersion}", fullVersion);
            return null;
        }

        // Also process install_profile.json to get installer libraries
        var installProfileEntry = archive.Entries.FirstOrDefault(e => e.FullName == "install_profile.json");
        if (installProfileEntry != null)
        {
            using var ipStream = installProfileEntry.Open();
            var ipJson = await new StreamReader(ipStream).ReadToEndAsync(ct);
            var installProfile = JsonSerializer.Deserialize<ForgeInstallProfile>(ipJson, JsonOptions);

            if (installProfile?.Libraries != null)
            {
                // Download installer-only libraries (they may not be in version.json)
                await DownloadForgeLibrariesAsync(installProfile.Libraries, ct);
            }
        }

        // Extract bundled libraries from the installer JAR itself
        await ExtractBundledLibrariesAsync(archive, ct);

        _logger.Information("Forge {FullVersion} profile ready: mainClass={MainClass}", fullVersion, profile.MainClass);
        return profile;
    }

    private async Task<MinecraftVersion?> ProcessOldFormatAsync(
        ZipArchive archive,
        ZipArchiveEntry profileEntry,
        string fullVersion,
        CancellationToken ct)
    {
        _logger.Information("Processing Forge {FullVersion} (old format)", fullVersion);

        using var stream = profileEntry.Open();
        var json = await new StreamReader(stream).ReadToEndAsync(ct);
        var installProfile = JsonSerializer.Deserialize<ForgeInstallProfile>(json, JsonOptions);

        if (installProfile?.VersionInfo == null)
        {
            _logger.Error("Old-format Forge installer {FullVersion} missing versionInfo", fullVersion);
            return null;
        }

        var versionInfo = installProfile.VersionInfo;

        // Convert old format to MinecraftVersion
        var profile = new MinecraftVersion
        {
            Id = versionInfo.Id ?? fullVersion,
            MainClass = versionInfo.MainClass ?? "net.minecraft.launchwrapper.Launch",
            InheritsFrom = versionInfo.InheritsFrom,
            Type = versionInfo.Type ?? "release"
        };

        // Convert old-style libraries
        if (versionInfo.Libraries != null)
        {
            profile.Libraries = ConvertOldLibraries(versionInfo.Libraries);
        }

        // Convert legacy minecraftArguments to Arguments structure
        if (!string.IsNullOrEmpty(versionInfo.MinecraftArguments))
        {
            profile.MinecraftArguments = versionInfo.MinecraftArguments;
        }

        // Extract bundled Forge universal JAR
        await ExtractBundledLibrariesAsync(archive, ct);

        _logger.Information("Forge {FullVersion} (old format) profile ready", fullVersion);
        return profile;
    }

    private async Task DownloadForgeLibrariesAsync(List<ForgeLibrary> libraries, CancellationToken ct)
    {
        foreach (var lib in libraries)
        {
            if (lib.Downloads?.Artifact == null) continue;
            var artifact = lib.Downloads.Artifact;
            if (string.IsNullOrEmpty(artifact.Url) || string.IsNullOrEmpty(artifact.Path))
                continue;

            var localPath = Path.Combine(_config.LibrariesDir, artifact.Path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(localPath))
            {
                // Verify hash if available
                if (!string.IsNullOrEmpty(artifact.Sha1))
                {
                    var existing = await CryptoUtils.CalculateFileSHA1Async(localPath, ct);
                    if (string.Equals(existing, artifact.Sha1, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                else
                {
                    continue;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            _logger.Debug("Downloading Forge library: {Path}", artifact.Path);
            var (resp, _) = await _httpManager.DownloadAsync(artifact.Url, localPath, cancellationToken: ct);
            if (!resp.IsSuccessStatusCode)
                _logger.Warning("Failed to download Forge library {Path}: {Status}", artifact.Path, resp.StatusCode);
        }
    }

    private async Task ExtractBundledLibrariesAsync(ZipArchive archive, CancellationToken ct)
    {
        // Forge installers bundle some libraries under "maven/" in the JAR
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith("maven/", StringComparison.OrdinalIgnoreCase))
                continue;
            if (entry.FullName.EndsWith("/"))
                continue;

            var relPath = entry.FullName.Substring("maven/".Length).Replace('/', Path.DirectorySeparatorChar);
            var destPath = Path.Combine(_config.LibrariesDir, relPath);

            if (File.Exists(destPath))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            ct.ThrowIfCancellationRequested();

            await using var entryStream = entry.Open();
            await using var fs = File.Create(destPath);
            await entryStream.CopyToAsync(fs, ct);
            _logger.Debug("Extracted bundled library: {Path}", relPath);
        }
    }

    private static List<Library> ConvertOldLibraries(List<ForgeLibraryOld> oldLibs)
    {
        var result = new List<Library>();
        foreach (var old in oldLibs)
        {
            if (string.IsNullOrEmpty(old.Name)) continue;

            var lib = new Library
            {
                Name = old.Name,
                Url = old.Url
            };
            result.Add(lib);
        }
        return result;
    }
}
