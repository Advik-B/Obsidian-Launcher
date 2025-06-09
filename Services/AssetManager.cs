// Services/AssetManager.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services;

public class AssetManager
{
    private const string MinecraftResourcesUrlBase = "https://resources.download.minecraft.net/";
    private readonly LauncherConfig _config;
    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;

    public AssetManager(LauncherConfig config, HttpManager httpManager)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _logger = LogHelper.GetLogger<AssetManager>();
        _logger.Verbose("AssetManager initialized.");
    }

    /// <summary>
    ///     Ensures all assets for the given Minecraft version are downloaded and verified.
    /// </summary>
    /// <param name="mcVersion">The Minecraft version details.</param>
    /// <param name="progress">Optional progress reporter for overall asset download progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all assets are successfully processed, false otherwise.</returns>
    public async Task<bool> EnsureAssetsAsync(
        MinecraftVersion mcVersion,
        IProgress<AssetDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (mcVersion.AssetIndex == null && string.IsNullOrEmpty(mcVersion.Assets))
        {
            _logger.Warning(
                "Version {VersionId} has no AssetIndex and no fallback 'assets' string. Cannot process assets.",
                mcVersion.Id);
            return true; // No assets to process, so technically successful.
        }

        var currentAssetIndexMetadata = mcVersion.AssetIndex;
        var assetIndexId =
            mcVersion.AssetIndex?.Id ??
            mcVersion.Assets; // Use AssetIndex.Id if available, else fallback to mcVersion.Assets

        if (currentAssetIndexMetadata == null)
        {
            // This case might happen for very old versions that only have the "assets" string (e.g., "legacy")
            // and don't point to a separate asset index JSON via assetIndex.url.
            // For now, we'll assume modern versions always have an assetIndex object.
            // If supporting very old versions, this part would need to fetch the manifest
            // to find the URL for the "assets" string id.
            _logger.Warning(
                "Minecraft version {VersionId} does not have a direct AssetIndex object. The 'assets' field is '{AssetsString}'. Advanced handling for this might be needed.",
                mcVersion.Id, mcVersion.Assets);
            // If assetIndexId is "legacy" or "pre-1.6", specific handling is needed which is complex.
            Debug.Assert(assetIndexId != null, nameof(assetIndexId) + " != null");
            if (assetIndexId.Equals("legacy", StringComparison.OrdinalIgnoreCase) ||
                assetIndexId.Equals("pre-1.6", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Information(
                    "Legacy assets ('{AssetIndexId}') require special handling (copying from client JAR or specific download logic not implemented in this basic manager). Skipping asset download.",
                    assetIndexId);
                return true; // Consider this "successful" as there's no standard index to process.
            }

            // If it's a modern ID but the AssetIndex object was missing, that's an error in the version JSON or our parsing.
            _logger.Error(
                "AssetIndex object is missing for version {VersionId}, but 'assets' field ('{AssetsString}') is not a known legacy type. Cannot proceed.",
                mcVersion.Id, mcVersion.Assets);
            return false;
        }

        _logger.Information("Processing assets for index ID: {AssetIndexId}, URL: {AssetIndexUrl}",
            currentAssetIndexMetadata.Id, currentAssetIndexMetadata.Url);

        var assetIndexFilePath = Path.Combine(_config.AssetIndexesDir, $"{currentAssetIndexMetadata.Id}.json");

        // 1. Download or verify the Asset Index JSON file
        var assetIndexValid = await DownloadAndVerifyFileAsync(
            currentAssetIndexMetadata.Url,
            assetIndexFilePath,
            currentAssetIndexMetadata.Sha1,
            "Asset Index JSON",
            cancellationToken);

        if (!assetIndexValid)
        {
            _logger.Error("Failed to obtain a valid asset index JSON for {AssetIndexId}.",
                currentAssetIndexMetadata.Id);
            return false;
        }

        // 2. Parse the Asset Index JSON
        AssetIndexDetails? assetIndexDetails;
        try
        {
            var indexJsonContent = await File.ReadAllTextAsync(assetIndexFilePath, cancellationToken);
            assetIndexDetails = JsonSerializer.Deserialize<AssetIndexDetails>(indexJsonContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (assetIndexDetails?.Objects == null)
            {
                _logger.Error(
                    "Failed to parse asset index JSON for {AssetIndexId} or 'objects' map is missing. File: {FilePath}",
                    currentAssetIndexMetadata.Id, assetIndexFilePath);
                return false;
            }

            _logger.Information("Successfully parsed asset index for {AssetIndexId}. Found {Count} asset objects.",
                currentAssetIndexMetadata.Id, assetIndexDetails.Objects.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception while reading or parsing asset index JSON {FilePath} for {AssetIndexId}.",
                assetIndexFilePath, currentAssetIndexMetadata.Id);
            return false;
        }

        // 3. Iterate and download/verify individual assets
        var totalAssets = assetIndexDetails.Objects.Count;
        var processedAssets = 0;
        var successfullyProcessedAssets = 0;

        // Determine base asset objects directory
        var assetObjectsDir = _config.AssetObjectsDir;
        if (assetIndexDetails.IsVirtual || assetIndexDetails.MapToResources)
            // Legacy versions might store assets in a "virtual/legacy" or "resources" subdirectory.
            // For "virtual": assets are typically in assets/virtual/<asset_index_id>/<virtual_path>
            // For "map_to_resources": assets are in assets/resources/<virtual_path> (very old pre-1.6)
            // This basic manager will use the modern HASH-based storage for simplicity,
            // but a full launcher would need to respect these flags for path construction if copying/symlinking.
            // For now, we just log it.
            _logger.Information(
                "Asset index {AssetIndexId} is marked as virtual ({IsVirtual}) or map_to_resources ({MapToResources}). Using modern hash-based storage.",
                currentAssetIndexMetadata.Id, assetIndexDetails.IsVirtual, assetIndexDetails.MapToResources);


        var downloadTasks = new List<Task<bool>>();
        var maxConcurrentDownloads = Environment.ProcessorCount; // Or a configurable value

        foreach (var assetEntry in assetIndexDetails.Objects)
        {
            //string virtualPath = assetEntry.Key; // e.g., "minecraft/textures/block/stone.png"
            var assetInfo = assetEntry.Value;

            var assetHash = assetInfo.Hash;
            var subDir = assetHash.Substring(0, 2);
            var assetFilename = assetHash;
            var assetObjectPath = Path.Combine(assetObjectsDir, subDir, assetFilename);
            var assetDownloadUrl = $"{MinecraftResourcesUrlBase}{subDir}/{assetHash}";

            // Simple concurrency limiting
            while (downloadTasks.Count(t => !t.IsCompleted) >= maxConcurrentDownloads)
            {
                await Task.WhenAny(downloadTasks.Where(t => !t.IsCompleted));
                cancellationToken.ThrowIfCancellationRequested();
            }

            var downloadTask = Task.Run(async () =>
            {
                var success = await DownloadAndVerifyFileAsync(
                    assetDownloadUrl,
                    assetObjectPath,
                    assetHash,
                    $"Asset {assetHash}", // virtualPath could be used for more descriptive logging
                    cancellationToken,
                    assetInfo.Size);

                Interlocked.Increment(ref processedAssets);
                if (success) Interlocked.Increment(ref successfullyProcessedAssets);
                progress?.Report(new AssetDownloadProgress
                {
                    CurrentFile = Path.GetFileName(assetObjectPath), // or virtualPath
                    TotalFiles = totalAssets,
                    ProcessedFiles = Interlocked.CompareExchange(ref processedAssets, 0, 0), // Read current value
                    CurrentFileBytesDownloaded = success ? (long)assetInfo.Size : 0,
                    CurrentFileTotalBytes = (long)assetInfo.Size
                });
                return success;
            }, cancellationToken);
            downloadTasks.Add(downloadTask);
        }

        await Task.WhenAll(downloadTasks).ConfigureAwait(false);

        var allSucceeded = successfullyProcessedAssets == totalAssets;
        if (allSucceeded)
            _logger.Information("All {TotalAssets} assets for index {AssetIndexId} are present and verified.",
                totalAssets, currentAssetIndexMetadata.Id);
        else
            _logger.Error(
                "{FailedCount} out of {TotalAssets} assets failed to download or verify for index {AssetIndexId}.",
                totalAssets - successfullyProcessedAssets, totalAssets, currentAssetIndexMetadata.Id);

        return allSucceeded;
    }

    /// <summary>
    ///     Downloads a file if it doesn't exist or if its SHA1 hash doesn't match.
    /// </summary>
    /// <returns>True if the file is valid (exists and matches hash, or successfully downloaded and verified).</returns>
    internal async Task<bool> DownloadAndVerifyFileAsync(
        string url,
        string localPath,
        string expectedSha1,
        string fileDescription, // For logging
        CancellationToken cancellationToken,
        ulong? expectedSize = null) // Optional expected size for more robust check before download
    {
        _logger.Verbose("Ensuring file: {Description} -> {LocalPath} from {Url}", fileDescription, localPath, url);

        var fileInfo = new FileInfo(localPath);

        if (fileInfo.Exists)
        {
            if (expectedSize.HasValue && fileInfo.Length != (long)expectedSize.Value)
            {
                _logger.Warning(
                    "File {Description} exists at {LocalPath} but size mismatch. Expected: {ExpectedSize}, Actual: {ActualSize}. Re-downloading.",
                    fileDescription, localPath, expectedSize.Value, fileInfo.Length);
            }
            else if (!string.IsNullOrEmpty(expectedSha1))
            {
                _logger.Verbose("Verifying SHA1 for existing file: {LocalPath}", localPath);
                string? actualSha1 = await CryptoUtils.CalculateFileSHA1Async(localPath, cancellationToken);
                if (cancellationToken.IsCancellationRequested) return false;

                Debug.Assert(actualSha1 != null, nameof(actualSha1) + " != null");
                if (actualSha1.Equals(expectedSha1, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Verbose("SHA1 match for existing file: {LocalPath}. No download needed.", localPath);
                    return true;
                }

                _logger.Warning(
                    "SHA1 mismatch for existing file {Description} at {LocalPath}. Expected: {ExpectedSha1}, Actual: {ActualSha1}. Re-downloading.",
                    fileDescription, localPath, expectedSha1, actualSha1);
            }
            else
            {
                // No SHA1 to verify, and size matches or not provided, assume it's fine.
                _logger.Verbose(
                    "File {Description} exists and no SHA1 provided for verification, or size matches. Assuming valid: {LocalPath}",
                    fileDescription, localPath);
                return true;
            }

            // If we reach here, it's because of size mismatch or SHA1 mismatch, so delete and re-download
            try
            {
                fileInfo.Delete();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete mismatched file {LocalPath} before re-download.", localPath);
                return false;
            }
        }

        _logger.Verbose("Downloading {Description}: {Url} -> {LocalPath}", fileDescription, url, localPath);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        var (response, downloadedFilePath) =
            await _httpManager.DownloadAsync(url, localPath, null /* IProgress can be added */, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            DeletePartialFile(downloadedFilePath, "Download Canceled");
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Failed to download {Description} from {Url}. Status: {StatusCode}. File: {LocalPath}",
                fileDescription, url, response.StatusCode, localPath);
            DeletePartialFile(downloadedFilePath, $"HTTP Error {response.StatusCode}");
            return false;
        }

        _logger.Verbose("Download complete for {Description}: {LocalPath}", fileDescription, localPath);

        if (!string.IsNullOrEmpty(expectedSha1))
        {
            _logger.Verbose("Verifying SHA1 for downloaded file: {LocalPath}", localPath);
            var actualSha1 = await CryptoUtils.CalculateFileSHA1Async(localPath, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                DeletePartialFile(localPath, "Verification Canceled");
                return false;
            }

            if (!actualSha1.Equals(expectedSha1, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Error(
                    "SHA1 mismatch after downloading {Description} to {LocalPath}. Expected: {ExpectedSha1}, Actual: {ActualSha1}",
                    fileDescription, localPath, expectedSha1, actualSha1);
                DeletePartialFile(localPath, "SHA1 Mismatch");
                return false;
            }

            _logger.Verbose("SHA1 verified for downloaded file: {LocalPath}", localPath);
        }

        return true;
    }

    private void DeletePartialFile(string filePath, string reason)
    {
        if (File.Exists(filePath))
            try
            {
                File.Delete(filePath);
                _logger.Warning("Deleted file {FilePath} due to: {Reason}", filePath, reason);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete file {FilePath} after error ({Reason})", filePath, reason);
            }
    }

    public async Task<string?>? EnsureClientJarAsync(MinecraftVersion mcVersion, CancellationToken cancellationToken)
    {
        _logger.Information("Ensuring Client JAR for Minecraft {VersionId}", mcVersion.Id);

        // Client JAR is stored globally in the versions directory
        var globalVersionStoreDir = Path.Combine(_config.VersionsDir, mcVersion.Id);
        Directory.CreateDirectory(globalVersionStoreDir); // Ensure this specific version's global dir exists
        var clientJarPath = Path.Combine(globalVersionStoreDir, $"{mcVersion.Id}.jar");

        var clientJarOk = false; // I didn't even use this variable lmfao
        if (mcVersion.Downloads.TryGetValue("client", out var clientDownloadDetails))
        {
            clientJarOk = await DownloadAndVerifyFileAsync(
                clientDownloadDetails.Url,
                clientJarPath,
                clientDownloadDetails.Sha1,
                $"Client JAR for {mcVersion.Id}",
                cancellationToken,
                clientDownloadDetails.Size);
        }
        else
        {
            _logger.Error("No client JAR download information found for version {VersionId}.", mcVersion.Id);
            return null;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.Warning("Client JAR download cancelled for {VersionId}.", mcVersion.Id);
            return null;
        }

        if (!clientJarOk)
        {
            _logger.Error("Failed to download or verify client JAR for version {VersionId}.", mcVersion.Id);
            return null;
        }

        _logger.Information("Client JAR for version {VersionId} is ready at global path {ClientJarPath}", mcVersion.Id,
            clientJarPath);
        return Path.GetFullPath(clientJarPath);
    }
}

/// <summary>
///     Progress report structure for asset downloads.
/// </summary>
public class AssetDownloadProgress
{
    public string? CurrentFile { get; set; }
    public int ProcessedFiles { get; set; }
    public int TotalFiles { get; set; }
    public long CurrentFileBytesDownloaded { get; set; } // For the currently downloading file
    public long CurrentFileTotalBytes { get; set; } // For the currently downloading file
}