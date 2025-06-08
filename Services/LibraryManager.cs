// Services/LibraryManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression; // For ZipFile and ZipArchive
using System.Linq;
using System.Net.Http;
using System.Text.Json; // Not directly used here, but models might be
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using ObsidianLauncher.Enums;
using Serilog;

namespace ObsidianLauncher.Services
{
    public class LibraryManager
    {
        private readonly LauncherConfig _config;
        private readonly HttpManager _httpManager;
        private readonly ILogger _logger;

        public LibraryManager(LauncherConfig config, HttpManager httpManager)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
            _logger = LogHelper.GetLogger<LibraryManager>();
            _logger.Verbose("LibraryManager initialized.");
        }

        /// <summary>
        /// Ensures all applicable libraries for the given Minecraft version are downloaded, verified, and natives extracted.
        /// </summary>
        /// <param name="mcVersion">The Minecraft version details.</param>
        /// <param name="nativesDir">The directory where native libraries should be extracted.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of paths to all applicable library JARs (for the classpath). Returns null on critical failure.</returns>
        public async Task<List<string>> EnsureLibrariesAsync(
            MinecraftVersion mcVersion,
            string nativesDir, // e.g., <version_dir>/<version_id>-natives
            IProgress<LibraryProcessingProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (mcVersion.Libraries == null || !mcVersion.Libraries.Any())
            {
                _logger.Information("No libraries listed for version {VersionId}.", mcVersion.Id);
                return new List<string>(); // No libraries to process, success.
            }

            _logger.Information("Processing {Count} library entries for version {VersionId}...", mcVersion.Libraries.Count, mcVersion.Id);
            Directory.CreateDirectory(_config.LibrariesDir); // Ensure base libraries directory exists
            Directory.CreateDirectory(nativesDir);     // Ensure natives directory exists

            var classpathEntries = new List<string>();
            int totalLibraries = mcVersion.Libraries.Count;
            int processedLibraries = 0;
            int successfullyProcessedLibraries = 0;

            // Could use Task.WhenAll for concurrency, but library processing often has interdependencies
            // or might be fine sequentially unless there are many independent large downloads.
            // For simplicity, processing sequentially first. Can be parallelized later if needed.
            foreach (var library in mcVersion.Libraries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processedLibraries++;
                ReportLibraryProgress(progress, library.Name, processedLibraries, totalLibraries, "Checking applicability...");

                if (!IsLibraryApplicable(library))
                {
                    _logger.Verbose("Skipping library (not applicable by rules): {LibraryName}", library.Name);
                    ReportLibraryProgress(progress, library.Name, processedLibraries, totalLibraries, "Skipped (Rules)");
                    continue;
                }

                _logger.Verbose("Processing library: {LibraryName}", library.Name);

                // 1. Handle main artifact
                bool mainArtifactOk = true;
                if (library.Downloads?.Artifact != null)
                {
                    var artifact = library.Downloads.Artifact;
                    string artifactLocalPath = Path.Combine(_config.LibrariesDir, artifact.Path.Replace('/', Path.DirectorySeparatorChar));

                    ReportLibraryProgress(progress, library.Name, processedLibraries, totalLibraries, $"Ensuring artifact: {Path.GetFileName(artifactLocalPath)}");
                    mainArtifactOk = await DownloadAndVerifyFileAsync(
                        artifact.Url,
                        artifactLocalPath,
                        artifact.Sha1,
                        $"Library artifact {library.Name}",
                        cancellationToken,
                        artifact.Size
                    );

                    if (mainArtifactOk)
                    {
                        classpathEntries.Add(Path.GetFullPath(artifactLocalPath)); // Add to classpath
                        _logger.Verbose("Main artifact for {LibraryName} is ready at {Path}", library.Name, artifactLocalPath);
                    }
                    else
                    {
                        _logger.Error("Failed to ensure main artifact for library {LibraryName}. Path: {Path}", library.Name, artifactLocalPath);
                        // Decide if this is a critical failure or if we can continue
                        // For now, let's assume it's critical for this library.
                    }
                }
                else if (library.Downloads?.Classifiers == null || !library.Downloads.Classifiers.Any())
                {
                     _logger.Verbose("Library {LibraryName} has no specified artifact or classifiers in downloads. Assuming it's a conditional/platform-specific parent or already provided.", library.Name);
                    // This library might be a "parent" POM or only provide natives.
                }


                // 2. Handle natives
                bool nativesOk = true;
                if (mainArtifactOk && library.Natives != null && library.Natives.Any())
                {
                    string osName = GetCurrentOsNameForNatives();
                    if (library.Natives.TryGetValue(osName, out string nativeClassifierKey))
                    {
                        if (library.Downloads?.Classifiers != null &&
                            library.Downloads.Classifiers.TryGetValue(nativeClassifierKey, out LibraryArtifact nativeArtifact))
                        {
                            string nativeJarLocalPath = Path.Combine(_config.LibrariesDir, nativeArtifact.Path.Replace('/', Path.DirectorySeparatorChar));
                            ReportLibraryProgress(progress, library.Name, processedLibraries, totalLibraries, $"Ensuring native: {nativeClassifierKey}");

                            bool nativeJarDownloaded = await DownloadAndVerifyFileAsync(
                                nativeArtifact.Url,
                                nativeJarLocalPath,
                                nativeArtifact.Sha1,
                                $"Native library {library.Name} ({nativeClassifierKey})",
                                cancellationToken,
                                nativeArtifact.Size
                            );

                            if (nativeJarDownloaded)
                            {
                                _logger.Information("Extracting natives for {LibraryName} from {NativeJarPath} to {NativesDir}",
                                    library.Name, nativeJarLocalPath, nativesDir);
                                ReportLibraryProgress(progress, library.Name, processedLibraries, totalLibraries, $"Extracting native: {nativeClassifierKey}");
                                nativesOk = ExtractNativeJar(nativeJarLocalPath, nativesDir, library.Extract);
                                if (!nativesOk)
                                {
                                    _logger.Error("Failed to extract natives for library {LibraryName} from {NativeJarPath}", library.Name, nativeJarLocalPath);
                                }
                                else
                                {
                                     _logger.Verbose("Natives for {LibraryName} extracted successfully.", library.Name);
                                }
                            }
                            else
                            {
                                _logger.Error("Failed to ensure native JAR for library {LibraryName} ({NativeClassifierKey})", library.Name, nativeClassifierKey);
                                nativesOk = false;
                            }
                        }
                        else
                        {
                            _logger.Warning("Native classifier '{NativeClassifierKey}' specified for OS '{OsName}' in library {LibraryName}, but no corresponding download found in classifiers.",
                                nativeClassifierKey, osName, library.Name);
                            // This might not be an error if the main artifact itself contains natives for some OSes,
                            // but usually, if `natives` map is present, a classifier is expected.
                        }
                    }
                    else
                    {
                        _logger.Verbose("No specific native classifier for current OS '{OsName}' in library {LibraryName}.", osName, library.Name);
                    }
                }

                if (mainArtifactOk && nativesOk) // Only count as successful if both main (if any) and natives (if any) are okay
                {
                    successfullyProcessedLibraries++;
                    ReportLibraryProgress(progress, library.Name, processedLibraries, totalLibraries, "Processed successfully");
                }
                else
                {
                     ReportLibraryProgress(progress, library.Name, processedLibraries, totalLibraries, "Processing failed");
                    // If one library fails, should we stop the whole process? For now, we continue but report overall failure.
                }
            }

            bool allSucceeded = successfullyProcessedLibraries == mcVersion.Libraries.Count(IsLibraryApplicable);
            if (allSucceeded)
            {
                _logger.Information("All {SuccessfullyProcessedCount} applicable libraries for version {VersionId} processed successfully.",
                    successfullyProcessedLibraries, mcVersion.Id);
            }
            else
            {
                _logger.Error("{FailedCount} out of {ApplicableCount} applicable libraries failed to process for version {VersionId}.",
                    mcVersion.Libraries.Count(IsLibraryApplicable) - successfullyProcessedLibraries,
                    mcVersion.Libraries.Count(IsLibraryApplicable), mcVersion.Id);
                return null; // Indicate a critical failure in library setup
            }

            return classpathEntries;
        }

        private bool IsLibraryApplicable(Library library)
        {
            if (library.Rules == null || !library.Rules.Any())
            {
                return true; // No rules means allow by default
            }

            // Default to disallow if rules are present, unless an allow rule matches
            bool allowed = false;

            foreach (var rule in library.Rules)
            {
                bool conditionMet = true;

                if (rule.Os != null)
                {
                    if (!CheckOsRule(rule.Os))
                    {
                        conditionMet = false;
                    }
                }
                // `features` rules are not implemented in this basic version,
                // as feature detection logic is application-specific.
                // if (rule.Features != null && conditionMet) { /* ... feature check logic ... */ }

                if (conditionMet)
                {
                    if (rule.Action == RuleAction.Allow)
                    {
                        allowed = true;
                    }
                    else if (rule.Action == RuleAction.Disallow)
                    {
                        return false; // A disallow rule immediately makes it not applicable
                    }
                }
            }
            return allowed;
        }

        private bool CheckOsRule(OperatingSystemInfo osRule)
        {
            if (osRule == null) return true; // No OS specific rule part

            bool nameMatch = true;
            if (!string.IsNullOrEmpty(osRule.Name))
            {
                var currentOs = OsUtils.GetCurrentOS();
                string currentOsName = "";
                switch (currentOs)
                {
                    case OperatingSystemType.Windows: currentOsName = "windows"; break;
                    case OperatingSystemType.MacOS: currentOsName = "osx"; break; // Mojang uses "osx"
                    case OperatingSystemType.Linux: currentOsName = "linux"; break;
                }
                nameMatch = osRule.Name.Equals(currentOsName, StringComparison.OrdinalIgnoreCase);
            }

            bool versionMatch = true;
            if (!string.IsNullOrEmpty(osRule.Version) && nameMatch) // Only check version if OS name matches
            {
                // OS Version matching with regex is complex and platform-dependent.
                // System.Environment.OSVersion.VersionString can be used, but its format varies.
                // For simplicity, this basic implementation doesn't perform regex version matching.
                // A real implementation would need a robust way to check OS version against regex.
                _logger.Verbose("OS version rule found ('{OsRuleVersion}') but not implemented for matching against '{CurrentOsVersion}'. Assuming match for now.",
                    osRule.Version, Environment.OSVersion.VersionString);
                // versionMatch = Regex.IsMatch(Environment.OSVersion.VersionString, osRule.Version);
            }
            
            // Arch matching for libraries is less common in the 'os' rule for the library itself,
            // usually handled by 'natives' and classifiers. But if present:
            bool archMatch = true;
            if (!string.IsNullOrEmpty(osRule.Arch) && nameMatch)
            {
                 var currentArch = OsUtils.GetCurrentArchitecture().ToString().ToLowerInvariant();
                 // Common arch strings in rules: "x86", "x64"
                 archMatch = osRule.Arch.Equals(currentArch, StringComparison.OrdinalIgnoreCase) ||
                            (osRule.Arch == "x86" && currentArch == "x86") || // Explicit check
                            (osRule.Arch == "x64" && currentArch == "x64");
            }


            return nameMatch && versionMatch && archMatch;
        }

        private string GetCurrentOsNameForNatives()
        {
            switch (OsUtils.GetCurrentOS())
            {
                case OperatingSystemType.Windows: return "windows";
                case OperatingSystemType.MacOS: return "osx";
                case OperatingSystemType.Linux: return "linux";
                default: return "unknown";
            }
        }

        private bool ExtractNativeJar(string nativeJarPath, string nativesDir, LibraryExtractRule extractRule)
        {
            try
            {
                using ZipArchive archive = ZipFile.OpenRead(nativeJarPath);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (extractRule?.Exclude != null)
                    {
                        bool excluded = false;
                        foreach (string exclusion in extractRule.Exclude)
                        {
                            // Simple prefix match, or ends with / for directory.
                            // More robust matching might be needed (e.g. Ant-style patterns)
                            if (entry.FullName.StartsWith(exclusion, StringComparison.OrdinalIgnoreCase))
                            {
                                excluded = true;
                                break;
                            }
                        }
                        if (excluded)
                        {
                            _logger.Verbose("Excluding native entry due to rule: {EntryFullName}", entry.FullName);
                            continue;
                        }
                    }

                    // Don't extract directories explicitly, ExtractToFile handles path creation.
                    if (string.IsNullOrEmpty(entry.Name)) // Typically indicates a directory
                    {
                        continue;
                    }

                    string destinationPath = Path.Combine(nativesDir, entry.Name); // Use entry.Name to avoid full path issues
                    
                    // Ensure the destination directory for the file exists
                    string entryDirectory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(entryDirectory) && !Directory.Exists(entryDirectory))
                    {
                        Directory.CreateDirectory(entryDirectory);
                    }

                    _logger.Verbose("Extracting native: {EntryFullName} to {DestinationPath}", entry.FullName, destinationPath);
                    entry.ExtractToFile(destinationPath, true); // true to overwrite
                }
                _logger.Information("Successfully extracted natives from {NativeJarPath}", nativeJarPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to extract native JAR: {NativeJarPath}", nativeJarPath);
                return false;
            }
        }


        // This helper is identical to the one in AssetManager, consider moving to a shared utility
        // or making AssetManager's version public static if appropriate.
        private async Task<bool> DownloadAndVerifyFileAsync(
            string url, string localPath, string expectedSha1, string fileDescription,
            CancellationToken cancellationToken, ulong? expectedSize = null)
        {
            _logger.Verbose("Ensuring file: {Description} -> {LocalPath} from {Url}", fileDescription, localPath, url);
            FileInfo fileInfo = new FileInfo(localPath);

            if (fileInfo.Exists)
            {
                if (expectedSize.HasValue && fileInfo.Length != (long)expectedSize.Value)
                {
                    _logger.Warning("File {Description} exists at {LocalPath} but size mismatch. Expected: {ExpectedSize}, Actual: {ActualSize}. Re-downloading.",
                        fileDescription, localPath, expectedSize.Value, fileInfo.Length);
                }
                else if (!string.IsNullOrEmpty(expectedSha1))
                {
                    _logger.Verbose("Verifying SHA1 for existing file: {LocalPath}", localPath);
                    string actualSha1 = await CryptoUtils.CalculateFileSHA1Async(localPath, cancellationToken);
                    if (cancellationToken.IsCancellationRequested) return false;
                    if (actualSha1 != null && actualSha1.Equals(expectedSha1, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Verbose("SHA1 match for existing file: {LocalPath}. No download needed.", localPath);
                        return true;
                    }
                    _logger.Warning("SHA1 mismatch for existing file {Description} at {LocalPath}. Expected: {ExpectedSha1}, Actual: {ActualSha1}. Re-downloading.",
                        fileDescription, localPath, expectedSha1, actualSha1 ?? "N/A");
                }
                else
                {
                    _logger.Verbose("File {Description} exists and no SHA1 provided for verification, or size matches. Assuming valid: {LocalPath}", fileDescription, localPath);
                    return true;
                }
                 try { fileInfo.Delete(); } catch(Exception ex) { _logger.Error(ex, "Failed to delete mismatched file {LocalPath} before re-download.", localPath); return false; }
            }

            _logger.Verbose("Downloading {Description}: {Url} -> {LocalPath}", fileDescription, url, localPath);
            string directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            var (response, downloadedFilePath) = await _httpManager.DownloadAsync(url, localPath, null, cancellationToken);
            if (cancellationToken.IsCancellationRequested) { DeletePartialFile(downloadedFilePath, "Download Canceled", fileDescription); return false; }
            if (!response.IsSuccessStatusCode)
            {
                _logger.Error("Failed to download {Description} from {Url}. Status: {StatusCode}. File: {LocalPath}",
                    fileDescription, url, response.StatusCode, localPath);
                DeletePartialFile(downloadedFilePath, $"HTTP Error {response.StatusCode}", fileDescription);
                return false;
            }
            _logger.Verbose("Download complete for {Description}: {LocalPath}", fileDescription, localPath);

            if (!string.IsNullOrEmpty(expectedSha1))
            {
                _logger.Verbose("Verifying SHA1 for downloaded file: {LocalPath}", localPath);
                string actualSha1 = await CryptoUtils.CalculateFileSHA1Async(localPath, cancellationToken);
                if (cancellationToken.IsCancellationRequested) { DeletePartialFile(localPath, "Verification Canceled", fileDescription); return false; }
                if (actualSha1 == null || !actualSha1.Equals(expectedSha1, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Error("SHA1 mismatch after downloading {Description} to {LocalPath}. Expected: {ExpectedSha1}, Actual: {ActualSha1}",
                        fileDescription, localPath, expectedSha1, actualSha1 ?? "N/A");
                    DeletePartialFile(localPath, "SHA1 Mismatch", fileDescription);
                    return false;
                }
                _logger.Verbose("SHA1 verified for downloaded file: {LocalPath}", localPath);
            }
            return true;
        }

        private void DeletePartialFile(string filePath, string reason, string fileDescription)
        {
            if (File.Exists(filePath))
            {
                try { File.Delete(filePath); _logger.Warning("Deleted file {Description} ({FilePath}) due to: {Reason}", fileDescription, filePath, reason); }
                catch (Exception ex) { _logger.Error(ex, "Failed to delete file {Description} ({FilePath}) after error ({Reason})", fileDescription, filePath, reason); }
            }
        }

         private void ReportLibraryProgress(IProgress<LibraryProcessingProgress> progress, string libraryName, int processed, int total, string status)
        {
            progress?.Report(new LibraryProcessingProgress
            {
                CurrentLibraryName = libraryName,
                ProcessedLibraries = processed,
                TotalLibraries = total,
                Status = status
            });
        }
    }

    /// <summary>
    /// Progress report structure for library processing.
    /// </summary>
    public class LibraryProcessingProgress
    {
        public string CurrentLibraryName { get; set; }
        public int ProcessedLibraries { get; set; }
        public int TotalLibraries { get; set; }
        public string Status { get; set; } // e.g., "Downloading", "Verifying", "Extracting", "Skipped"
    }
}