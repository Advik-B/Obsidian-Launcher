// Services/AssetManager.cs
using System;
using System.Collections.Generic;
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
    ///     Ensures all assets for the given launch profile are downloaded and verified.
    /// </summary>
    public async Task<bool> EnsureAssetsAsync(
        LaunchProfile launchProfile,
        IProgress<AssetDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (launchProfile.AssetIndex == null && string.IsNullOrEmpty(launchProfile.Assets))
        {
            _logger.Warning(
                "Version {VersionId} has no AssetIndex and no fallback 'assets' string. Cannot process assets.",
                launchProfile.Id);
            return true; // No assets to process, so technically successful.
        }

        var currentAssetIndexMetadata = launchProfile.AssetIndex;
        var assetIndexId =
            launchProfile.AssetIndex?.Id ??
            launchProfile.Assets;

        if (currentAssetIndexMetadata == null)
        {
            _logger.Error(
                "AssetIndex object is missing for version {VersionId}, but 'assets' field ('{AssetsString}') is not a known legacy type. Cannot proceed.",
                launchProfile.Id, launchProfile.Assets);
            return false;
        }

        _logger.Information("Processing assets for index ID: {AssetIndexId}, URL: {AssetIndexUrl}",
            currentAssetIndexMetadata.Id, currentAssetIndexMetadata.Url);

        var assetIndexFilePath = Path.Combine(_config.AssetIndexesDir, $"{currentAssetIndexMetadata.Id}.json");

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

        var totalAssets = assetIndexDetails.Objects.Count;
        var processedAssets = 0;
        var successfullyProcessedAssets = 0;

        var assetObjectsDir = _config.AssetObjectsDir;
        if (assetIndexDetails.IsVirtual || assetIndexDetails.MapToResources)
            _logger.Information(
                "Asset index {AssetIndexId} is marked as virtual ({IsVirtual}) or map_to_resources ({MapToResources}). Using modern hash-based storage.",
                currentAssetIndexMetadata.Id, assetIndexDetails.IsVirtual, assetIndexDetails.MapToResources);

        var semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
        var downloadTasks = new List<Task>();

        foreach (var assetEntry in assetIndexDetails.Objects)
        {
            var assetInfo = assetEntry.Value;
            var assetHash = assetInfo.Hash;
            var subDir = assetHash.Substring(0, 2);
            var assetObjectPath = Path.Combine(assetObjectsDir, subDir, assetHash);
            var assetDownloadUrl = $"{MinecraftResourcesUrlBase}{subDir}/{assetHash}";

            await semaphore.WaitAsync(cancellationToken);
            downloadTasks.Add(Task.Run(async () =>
            {
                try
                {
                    var success = await DownloadAndVerifyFileAsync(
                        assetDownloadUrl,
                        assetObjectPath,
                        assetHash,
                        $"Asset {assetHash}",
                        cancellationToken,
                        assetInfo.Size);

                    Interlocked.Increment(ref processedAssets);
                    if (success) Interlocked.Increment(ref successfullyProcessedAssets);
                    progress?.Report(new AssetDownloadProgress
                    {
                        CurrentFile = Path.GetFileName(assetObjectPath),
                        TotalFiles = totalAssets,
                        ProcessedFiles = Interlocked.CompareExchange(ref processedAssets, 0, 0),
                        CurrentFileBytesDownloaded = success ? (long)assetInfo.Size : 0,
                        CurrentFileTotalBytes = (long)assetInfo.Size
                    });
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
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

    internal async Task<bool> DownloadAndVerifyFileAsync(
        string url,
        string localPath,
        string expectedSha1,
        string fileDescription,
        CancellationToken cancellationToken,
        ulong? expectedSize = null)
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
                var actualSha1 = await CryptoUtils.CalculateFileSHA1Async(localPath, cancellationToken);
                if (cancellationToken.IsCancellationRequested) return false;

                if (actualSha1 != null && actualSha1.Equals(expectedSha1, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Verbose("SHA1 match for existing file: {LocalPath}. No download needed.", localPath);
                    return true;
                }

                _logger.Warning(
                    "SHA1 mismatch for existing file {Description} at {LocalPath}. Expected: {ExpectedSha1}, Actual: {ActualSha1}. Re-downloading.",
                    fileDescription, localPath, expectedSha1, actualSha1 ?? "N/A");
            }
            else
            {
                _logger.Verbose(
                    "File {Description} exists and no SHA1 provided for verification, or size matches. Assuming valid: {LocalPath}",
                    fileDescription, localPath);
                return true;
            }

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

        var directory = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        var (response, downloadedFilePath) =
            await _httpManager.DownloadAsync(url, localPath, null, cancellationToken);
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

            if (actualSha1 == null || !actualSha1.Equals(expectedSha1, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Error(
                    "SHA1 mismatch after downloading {Description} to {LocalPath}. Expected: {ExpectedSha1}, Actual: {ActualSha1}",
                    fileDescription, localPath, expectedSha1, actualSha1 ?? "N/A");
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

    public async Task<string?> EnsureClientJarAsync(LaunchProfile launchProfile, CancellationToken cancellationToken)
    {
        _logger.Information("Ensuring Client JAR for Minecraft {VersionId}", launchProfile.Id);

        var globalVersionStoreDir = Path.Combine(_config.VersionsDir, launchProfile.Id);
        Directory.CreateDirectory(globalVersionStoreDir);
        var clientJarPath = Path.Combine(globalVersionStoreDir, $"{launchProfile.Id}.jar");

        var clientJarOk = false;
        if (launchProfile.Downloads.TryGetValue("client", out var clientDownloadDetails))
        {
            clientJarOk = await DownloadAndVerifyFileAsync(
                clientDownloadDetails.Url,
                clientJarPath,
                clientDownloadDetails.Sha1,
                $"Client JAR for {launchProfile.Id}",
                cancellationToken,
                clientDownloadDetails.Size);
        }
        else
        {
            _logger.Error("No client JAR download information found for version {VersionId}.", launchProfile.Id);
            return null;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.Warning("Client JAR download cancelled for {VersionId}.", launchProfile.Id);
            return null;
        }

        if (!clientJarOk)
        {
            _logger.Error("Failed to download or verify client JAR for version {VersionId}.", launchProfile.Id);
            return null;
        }

        _logger.Information("Client JAR for version {VersionId} is ready at global path {ClientJarPath}", launchProfile.Id,
            clientJarPath);
        return Path.GetFullPath(clientJarPath);
    }
}