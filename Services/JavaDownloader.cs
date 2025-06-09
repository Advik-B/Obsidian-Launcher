using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;
// For FirstOrDefault and other LINQ operations
// Assuming your models are here (MinecraftVersion, JavaVersionInfo)
// For OsUtils, CryptoUtils
// For OperatingSystemType, ArchitectureType

namespace ObsidianLauncher.Services;

public class JavaDownloader
{
    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;

    public JavaDownloader(HttpManager httpManager)
    {
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _logger = LogHelper.GetLogger<JavaDownloader>();
        _logger.Verbose("JavaDownloader initialized.");
    }

    private async Task<JsonDocument> FetchMojangJavaManifestAsync(CancellationToken cancellationToken = default)
    {
        const string javaManifestUrl =
            "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";
        _logger.Information("Fetching Mojang Java runtime manifest from: {Url}", javaManifestUrl);

        var responseMsg = await _httpManager.GetAsync(javaManifestUrl, cancellationToken: cancellationToken);

        if (!responseMsg.IsSuccessStatusCode)
        {
            var errorContent = await responseMsg.Content.ReadAsStringAsync(cancellationToken);
            _logger.Error(
                "Failed to download Mojang Java runtime manifest. Status: {StatusCode}, URL: {Url}, Error: {ErrorContent}",
                responseMsg.StatusCode, javaManifestUrl, errorContent);
            return null;
        }

        var jsonString = await responseMsg.Content.ReadAsStringAsync(cancellationToken);
        _logger.Information("Successfully fetched Mojang Java manifest ({Length} bytes).", jsonString.Length);

        try
        {
            return JsonDocument.Parse(jsonString); // Keep as JsonDocument for flexible parsing
        }
        catch (JsonException ex)
        {
            _logger.Error(ex, "Failed to parse Mojang Java runtime manifest JSON. Content: {JsonString}", jsonString);
            return null;
        }
    }

    /// <summary>
    ///     Downloads the Java runtime specified in the Minecraft version manifest from Mojang.
    /// </summary>
    /// <param name="mcVersion">The Minecraft version details.</param>
    /// <param name="baseDownloadDir">The directory where the Java archive will be downloaded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The path to the downloaded Java archive, or null if an error occurred.</returns>
    public async Task<string> DownloadJavaForMinecraftVersionMojangAsync(
        MinecraftVersion mcVersion,
        string baseDownloadDir,
        CancellationToken cancellationToken = default)
    {
        if (mcVersion?.JavaVersion == null)
        {
            _logger.Information(
                "Minecraft version {VersionId} does not specify a Java version. Skipping Mojang Java download.",
                mcVersion?.Id ?? "Unknown");
            return null;
        }

        var requiredJava = mcVersion.JavaVersion;
        _logger.Information("Mojang Manifest - Required Java: Component '{Component}', Major Version '{MajorVersion}'",
            requiredJava.Component, requiredJava.MajorVersion);

        using var javaManifestDoc = await FetchMojangJavaManifestAsync(cancellationToken);
        if (javaManifestDoc == null) return null;

        var osArchKey = OsUtils.GetOSStringForJavaManifest();
        if (osArchKey == "unknown-os-arch-mojang")
        {
            _logger.Error("Mojang Manifest - Cannot determine OS/Arch string for Mojang manifest.");
            return null;
        }

        _logger.Information("Mojang Manifest - Determined OS/Arch key: {OsArchKey}", osArchKey);

        if (!javaManifestDoc.RootElement.TryGetProperty(osArchKey, out var osArchElement) ||
            !osArchElement.TryGetProperty(requiredJava.Component, out var componentElement) ||
            componentElement.ValueKind != JsonValueKind.Array)
        {
            _logger.Error(
                "Mojang Manifest - Java runtime for OS/Arch '{OsArchKey}' and component '{Component}' not found or not an array. Manifest Structure: {JsonStructure}",
                osArchKey, requiredJava.Component,
                javaManifestDoc.RootElement.ToString()
                    .Substring(0, Math.Min(500, javaManifestDoc.RootElement.ToString().Length)));
            return null;
        }

        string downloadUrl = null;
        string expectedSha1 = null;

        foreach (var entry in componentElement.EnumerateArray())
        {
            uint entryMajorVersion = 0;
            if (entry.TryGetProperty("version", out var versionElement) &&
                versionElement.TryGetProperty("name", out var nameElement))
            {
                if (nameElement.ValueKind == JsonValueKind.Number && nameElement.TryGetUInt32(out var numVersion))
                {
                    entryMajorVersion = numVersion;
                }
                else if (nameElement.ValueKind == JsonValueKind.String)
                {
                    var nameStr = nameElement.GetString();
                    var parts = nameStr.Split('.');
                    if (parts.Length > 0 && uint.TryParse(parts[0], out var parsedVersion))
                        entryMajorVersion = parsedVersion;
                    else
                        _logger.Warning(
                            "Could not parse major version from string in Mojang manifest entry's version name: {NameStr}",
                            nameStr);
                }
            }

            if (entryMajorVersion == requiredJava.MajorVersion)
                if (entry.TryGetProperty("manifest", out var manifestElement) &&
                    manifestElement.TryGetProperty("url", out var urlElement) &&
                    urlElement.ValueKind == JsonValueKind.String &&
                    manifestElement.TryGetProperty("sha1", out var sha1Element) &&
                    sha1Element.ValueKind == JsonValueKind.String)
                {
                    downloadUrl = urlElement.GetString();
                    expectedSha1 = sha1Element.GetString();
                    break;
                }
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            _logger.Error(
                "Mojang Manifest - Could not find download URL for Java '{Component}' v{MajorVersion} on {OsArchKey}",
                requiredJava.Component, requiredJava.MajorVersion, osArchKey);
            return null;
        }

        _logger.Information("Mojang Manifest - Found Java download URL: {DownloadUrl}", downloadUrl);

        var filename = Path.GetFileName(new Uri(downloadUrl).LocalPath); // Robust way to get filename from URL
        var downloadPath = Path.Combine(baseDownloadDir, filename);

        _logger.Information("Mojang Manifest - Downloading Java to: {DownloadPath}...", downloadPath);
        var (dlResponse, _) =
            await _httpManager.DownloadAsync(downloadUrl, downloadPath, cancellationToken: cancellationToken);

        if (!dlResponse.IsSuccessStatusCode)
        {
            _logger.Error("Mojang Manifest - Java archive download failed from {DownloadUrl}. Status: {StatusCode}",
                downloadUrl, dlResponse.StatusCode);
            return null;
        }

        _logger.Information("Mojang Manifest - Java downloaded successfully ({BytesDownloaded} bytes).",
            dlResponse.Content.Headers.ContentLength ?? -1);


        _logger.Information("Mojang Manifest - Verifying SHA1 hash for {Filename}...", Path.GetFileName(downloadPath));
        var actualSha1 = await CryptoUtils.CalculateFileSHA1Async(downloadPath, cancellationToken);
        if (string.IsNullOrEmpty(actualSha1))
        {
            _logger.Error("Mojang Manifest - SHA1 calculation failed for {DownloadPath}", downloadPath);
            if (File.Exists(downloadPath)) File.Delete(downloadPath);
            return null;
        }

        if (!actualSha1.Equals(expectedSha1, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Error(
                "Mojang Manifest - SHA1 hash mismatch! Expected: {ExpectedSha1}, Actual: {ActualSha1} for file {DownloadPath}",
                expectedSha1, actualSha1, downloadPath);
            if (File.Exists(downloadPath)) File.Delete(downloadPath);
            return null;
        }

        _logger.Information("Mojang Manifest - SHA1 hash verified for {Filename}.", Path.GetFileName(downloadPath));
        _logger.Information("Mojang Manifest - Java archive downloaded and verified: {DownloadPath}", downloadPath);
        return downloadPath;
    }

    /// <summary>
    ///     Downloads a specific Java major version from Adoptium API.
    /// </summary>
    /// <param name="requiredJava">Java version information (primarily MajorVersion is used).</param>
    /// <param name="baseDownloadDir">Directory to download the archive into.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to the downloaded archive, or null on failure.</returns>
    public async Task<string> DownloadJavaForSpecificVersionAdoptiumAsync(
        JavaVersionInfo requiredJava,
        string baseDownloadDir,
        CancellationToken cancellationToken = default)
    {
        _logger.Information("Adoptium API - Attempting to download Java. Required Major Version: {MajorVersion}",
            requiredJava.MajorVersion);

        var adoptiumOS = OsUtils.GetOSStringForAdoptium();
        var adoptiumArch = OsUtils.GetArchStringForAdoptium();

        if (string.IsNullOrEmpty(adoptiumOS) || string.IsNullOrEmpty(adoptiumArch))
        {
            _logger.Error("Adoptium API - Could not determine OS/Arch strings for Adoptium API call.");
            return null;
        }

        _logger.Information("Adoptium API - OS: {AdoptiumOS}, Arch: {AdoptiumArch}", adoptiumOS, adoptiumArch);

        var imageType = "jre"; // Common for launchers; "jdk" is also an option.
        var apiUrl = $"https://api.adoptium.net/v3/assets/latest/{requiredJava.MajorVersion}/hotspot" +
                     $"?architecture={adoptiumArch}" +
                     $"&heap_size=normal" + // "normal" or "large"
                     $"&image_type={imageType}" +
                     $"&os={adoptiumOS}" +
                     $"&vendor=eclipse"; // Common default; others: "temurin", "ibm"

        _logger.Information("Adoptium API - Querying: {ApiUrl}", apiUrl);
        var apiResponseMsg = await _httpManager.GetAsync(apiUrl, cancellationToken: cancellationToken);

        if (!apiResponseMsg.IsSuccessStatusCode)
        {
            var errorContent = await apiResponseMsg.Content.ReadAsStringAsync(cancellationToken);
            _logger.Error(
                "Adoptium API - Failed to query. Status: {StatusCode}, URL: {ApiUrl}, Error: \"{ErrorContent}\"",
                apiResponseMsg.StatusCode, apiUrl, errorContent);
            return null;
        }

        var jsonString = await apiResponseMsg.Content.ReadAsStringAsync(cancellationToken);
        _logger.Information("Adoptium API - Successfully queried API ({Length} bytes).", jsonString.Length);

        using var apiResponseDoc = JsonDocument.Parse(jsonString);
        if (apiResponseDoc.RootElement.ValueKind != JsonValueKind.Array ||
            apiResponseDoc.RootElement.GetArrayLength() == 0)
        {
            _logger.Error(
                "Adoptium API - No suitable builds found or unexpected JSON array format. Response: {ResponseJson}",
                jsonString.Substring(0, Math.Min(500, jsonString.Length)));
            return null;
        }

        var firstBuild = apiResponseDoc.RootElement[0]; // Take the first available build matching criteria

        if (!firstBuild.TryGetProperty("binary", out var binaryElement) ||
            !binaryElement.TryGetProperty("package", out var packageElement) ||
            !packageElement.TryGetProperty("link", out var linkElement) ||
            linkElement.ValueKind != JsonValueKind.String ||
            !packageElement.TryGetProperty("name", out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String ||
            !packageElement.TryGetProperty("checksum", out var checksumElement) ||
            checksumElement.ValueKind != JsonValueKind.String)
        {
            _logger.Error(
                "Adoptium API - Response JSON (first build entry) missing required fields 'binary.package.link', 'name', or 'checksum'. Build Entry: {FirstBuildJson}",
                firstBuild.ToString().Substring(0, Math.Min(500, firstBuild.ToString().Length)));
            return null;
        }

        var downloadUrl = linkElement.GetString();
        var filename = nameElement.GetString();
        var expectedSha256 = checksumElement.GetString();

        _logger.Information("Adoptium API - Found Java download URL: {DownloadUrl}", downloadUrl);
        _logger.Information("Filename: {Filename}, Expected SHA256: {ExpectedSha256}", filename, expectedSha256);

        Directory.CreateDirectory(baseDownloadDir); // Ensure directory exists
        var downloadPath = Path.Combine(baseDownloadDir, filename);

        _logger.Information("Adoptium API - Downloading Java to: {DownloadPath}...", downloadPath);
        var (dlResponse, _) =
            await _httpManager.DownloadAsync(downloadUrl, downloadPath, cancellationToken: cancellationToken);

        if (!dlResponse.IsSuccessStatusCode)
        {
            _logger.Error("Adoptium API - Java archive download failed from {DownloadUrl}. Status: {StatusCode}",
                downloadUrl, dlResponse.StatusCode);
            return null;
        }

        _logger.Information("Adoptium API - Java downloaded successfully ({BytesDownloaded} bytes).",
            dlResponse.Content.Headers.ContentLength ?? -1);

        _logger.Information("Adoptium API - Verifying SHA256 hash for {Filename}...", Path.GetFileName(downloadPath));
        var actualSha256 = await CryptoUtils.CalculateFileSHA256Async(downloadPath, cancellationToken);
        if (string.IsNullOrEmpty(actualSha256))
        {
            _logger.Error("Adoptium API - SHA256 calculation failed for {DownloadPath}", downloadPath);
            if (File.Exists(downloadPath)) File.Delete(downloadPath);
            return null;
        }

        if (!actualSha256.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Error(
                "Adoptium API - SHA256 hash mismatch! Expected: {ExpectedSha256}, Actual: {ActualSha256} for file {DownloadPath}",
                expectedSha256, actualSha256, downloadPath);
            if (File.Exists(downloadPath)) File.Delete(downloadPath);
            return null;
        }

        _logger.Information("Adoptium API - SHA256 hash verified for {Filename}.", Path.GetFileName(downloadPath));
        _logger.Information("Adoptium API - Java archive downloaded and verified: {DownloadPath}", downloadPath);
        return downloadPath;
    }
}