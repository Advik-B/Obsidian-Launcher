using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Models.InstallProfiles;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services.Installers;

public class ForgeInstaller : IModLoaderInstaller
{
    private const string ForgeMavenUrl = "https://maven.minecraftforge.net/net/minecraftforge/forge/";
    private readonly LauncherConfig _config;
    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;

    public string Name => "Forge";

    public ForgeInstaller(LauncherConfig config, HttpManager httpManager)
    {
        _config = config;
        _httpManager = httpManager;
        _logger = LogHelper.GetLogger<ForgeInstaller>();
    }

    /// <summary>
    /// Resolves friendly version names like "latest" or "recommended" to a specific build number.
    /// </summary>
    private async Task<string> ResolveForgeVersion(string minecraftVersion, string loaderVersion, CancellationToken cancellationToken)
    {
        if (!loaderVersion.Equals("latest", StringComparison.OrdinalIgnoreCase) && 
            !loaderVersion.Equals("recommended", StringComparison.OrdinalIgnoreCase))
        {
            return loaderVersion; // It's already a specific version number.
        }

        _logger.Information("[{LoaderName}] Resolving '{LoaderVersion}' for Minecraft {MCVersion}...", Name, loaderVersion, minecraftVersion);
        var promotionsUrl = $"{ForgeMavenUrl}promotions_slim.json";
        
        var response = await _httpManager.GetAsync(promotionsUrl, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("[{LoaderName}] Failed to fetch promotions manifest from {Url}. Status: {Status}", Name, promotionsUrl, response.StatusCode);
            return null;
        }

        try
        {
            var promoJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var promotions = JsonSerializer.Deserialize<ForgePromotions>(promoJson);
            
            var key = $"{minecraftVersion}-{loaderVersion}";
            if (promotions?.Promos != null && promotions.Promos.TryGetValue(key, out var resolvedVersion))
            {
                _logger.Information("[{LoaderName}] Resolved '{Key}' to version: {ResolvedVersion}", Name, key, resolvedVersion);
                return resolvedVersion;
            }

            _logger.Error("[{LoaderName}] Could not find promotion key '{Key}' in promotions manifest.", Name, key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[{LoaderName}] Failed to parse promotions manifest.", Name);
            return null;
        }
    }

    public async Task<string> InstallAsync(string minecraftVersion, string loaderVersion, CancellationToken cancellationToken = default)
    {
        // Step 1: Resolve "latest" or "recommended" to a concrete version number.
        var forgeVersion = await ResolveForgeVersion(minecraftVersion, loaderVersion, cancellationToken);
        if (string.IsNullOrEmpty(forgeVersion))
        {
            _logger.Error("[{LoaderName}] Could not resolve a valid Forge version for '{LoaderVersion}'. Halting installation.", Name, loaderVersion);
            return null;
        }

        // Step 2: Build paths and check cache
        var fullVersionString = $"{minecraftVersion}-{forgeVersion}";
        var installerFileName = $"forge-{fullVersionString}-installer.jar";
        var versionSpecificCacheDir = Path.Combine(_config.InstallerCacheDir, Name.ToLowerInvariant(), fullVersionString);
        var installerCachePath = Path.Combine(versionSpecificCacheDir, installerFileName);
        string installerPathToUse;

        Directory.CreateDirectory(versionSpecificCacheDir);

        if (File.Exists(installerCachePath))
        {
            _logger.Information("[{LoaderName}] Found cached installer: {Path}", Name, installerCachePath);
            installerPathToUse = installerCachePath;
        }
        else
        {
            // Step 3: Download installer if not cached.
            var installerUrl = $"{ForgeMavenUrl}{fullVersionString}/{installerFileName}";
            _logger.Information("[{LoaderName}] Installer not in cache. Downloading from: {Url}", Name, installerUrl);

            var (response, downloadedFilePath) = await _httpManager.DownloadAsync(installerUrl, installerCachePath, null, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Error("[{LoaderName}] Failed to download installer from guaranteed URL. Status: {Status}. This may indicate a network issue or that the specific version was removed.", Name, response.StatusCode);
                return null;
            }
            _logger.Information("[{LoaderName}] Installer successfully downloaded and cached at: {Path}", Name, downloadedFilePath);
            installerPathToUse = downloadedFilePath;
        }

        // Step 4: Extract and process install_profile.json
        ForgeInstallProfile installProfile;
        try
        {
            using var archive = ZipFile.OpenRead(installerPathToUse);
            var profileEntry = archive.GetEntry("install_profile.json");
            if (profileEntry == null)
            {
                _logger.Error("[{LoaderName}] Could not find 'install_profile.json' in the installer JAR.", Name);
                return null;
            }

            await using var stream = profileEntry.Open();
            installProfile = await JsonSerializer.DeserializeAsync<ForgeInstallProfile>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);

            if (string.IsNullOrEmpty(installProfile?.VersionInfo?.Id))
            {
                _logger.Error("[{LoaderName}] Failed to parse 'install_profile.json' or 'versionInfo.id' was missing.", Name);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[{LoaderName}] An error occurred while reading the installer JAR at {Path}.", Name, installerPathToUse);
            return null;
        }

        // Step 5: Create the new version file.
        var versionId = installProfile.VersionInfo.Id;
        var versionDir = Path.Combine(_config.VersionsDir, versionId);
        var versionJsonPath = Path.Combine(versionDir, $"{versionId}.json");

        if (File.Exists(versionJsonPath))
        {
            _logger.Information("[{LoaderName}] Version {VersionId} is already installed. Overwriting.", Name, versionId);
        }

        Directory.CreateDirectory(versionDir);

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var jsonString = JsonSerializer.Serialize(installProfile.VersionInfo, options);
            await File.WriteAllTextAsync(versionJsonPath, jsonString, cancellationToken);

            _logger.Information("[{LoaderName}] Successfully created version file: {Path}", Name, versionJsonPath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[{LoaderName}] Failed to write version file.", Name);
            return null;
        }

        _logger.Information("[{LoaderName}] Installation complete for version ID: '{VersionId}'", Name, versionId);
        return versionId;
    }
}