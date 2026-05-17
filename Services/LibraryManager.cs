using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Enums;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services;

public class LibraryManager
{
    private readonly LauncherConfig _config;
    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;
    private readonly AssetManager _assetManager;

    public LibraryManager(LauncherConfig config, HttpManager httpManager, AssetManager assetManager)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        _logger = LogHelper.GetLogger<LibraryManager>();
        _logger.Verbose("LibraryManager initialized.");
    }

    public async Task<List<string>?> EnsureLibrariesAsync(
        LaunchProfile launchProfile,
        string nativesDir,
        IProgress<LibraryProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (launchProfile.Libraries == null || !launchProfile.Libraries.Any())
        {
            _logger.Information("No libraries listed for version {VersionId}.", launchProfile.Id);
            return new List<string>();
        }

        _logger.Information("Processing {Count} library entries for version {VersionId}...", launchProfile.Libraries.Count, launchProfile.Id);
        Directory.CreateDirectory(_config.LibrariesDir);
        Directory.CreateDirectory(nativesDir);

        var classpathEntries = new List<string>();
        var totalLibraries = launchProfile.Libraries.Count;
        var processedLibraries = 0;
        var successfullyProcessedLibraries = 0;

        foreach (var library in launchProfile.Libraries)
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

            var mainArtifactOk = true;
            if (library.Downloads?.Artifact != null)
            {
                var artifact = library.Downloads.Artifact;
                var artifactLocalPath = Path.Combine(_config.LibrariesDir, artifact.Path.Replace('/', Path.DirectorySeparatorChar));

                ReportLibraryProgress(progress, library.Name, processedLibraries, totalLibraries, $"Ensuring artifact: {Path.GetFileName(artifactLocalPath)}");
                mainArtifactOk = await _assetManager.DownloadAndVerifyFileAsync(artifact.Url, artifactLocalPath, artifact.Sha1, $"Library artifact {library.Name}", cancellationToken, artifact.Size);

                if (mainArtifactOk)
                {
                    classpathEntries.Add(Path.GetFullPath(artifactLocalPath));
                    _logger.Verbose("Main artifact for {LibraryName} is ready at {Path}", library.Name, artifactLocalPath);
                }
                else
                {
                    _logger.Error("Failed to ensure main artifact for library {LibraryName}. Path: {Path}", library.Name, artifactLocalPath);
                }
            }
            else if (!string.IsNullOrEmpty(library.Url) && !string.IsNullOrEmpty(library.Name))
            {
                // Legacy Forge/mod-loader style: URL base + Maven path derived from name
                var mavenPath = MavenNameToPath(library.Name);
                var artifactLocalPath = Path.Combine(_config.LibrariesDir, mavenPath.Replace('/', Path.DirectorySeparatorChar));
                var downloadUrl = library.Url.TrimEnd('/') + "/" + mavenPath;

                ReportLibraryProgress(progress, library.Name, processedLibraries, totalLibraries, $"Ensuring legacy artifact: {Path.GetFileName(artifactLocalPath)}");
                mainArtifactOk = await _assetManager.DownloadAndVerifyFileAsync(downloadUrl, artifactLocalPath, string.Empty, $"Legacy library {library.Name}", cancellationToken);

                if (mainArtifactOk)
                {
                    classpathEntries.Add(Path.GetFullPath(artifactLocalPath));
                    _logger.Verbose("Legacy artifact for {LibraryName} is ready at {Path}", library.Name, artifactLocalPath);
                }
                else
                {
                    _logger.Error("Failed to ensure legacy artifact for library {LibraryName}. URL: {Url}", library.Name, downloadUrl);
                }
            }
            else if (library.Downloads?.Classifiers == null || !library.Downloads.Classifiers.Any())
            {
                _logger.Verbose("Library {LibraryName} has no specified artifact or classifiers in downloads. Assuming it's a conditional/platform-specific parent or already provided.", library.Name);
            }

            var nativesOk = true;
            if (mainArtifactOk && library.Natives != null && library.Natives.Any())
            {
                var osName = GetCurrentOsNameForNatives();
                if (library.Natives.TryGetValue(osName, out var nativeClassifierKey))
                {
                    if (library.Downloads?.Classifiers != null && library.Downloads.Classifiers.TryGetValue(nativeClassifierKey, out var nativeArtifact))
                    {
                        var nativeJarLocalPath = Path.Combine(_config.LibrariesDir, nativeArtifact.Path.Replace('/', Path.DirectorySeparatorChar));
                        ReportLibraryProgress(progress, library.Name, processedLibraries, totalLibraries, $"Ensuring native: {nativeClassifierKey}");

                        var nativeJarDownloaded = await _assetManager.DownloadAndVerifyFileAsync(nativeArtifact.Url, nativeJarLocalPath, nativeArtifact.Sha1, $"Native library {library.Name} ({nativeClassifierKey})", cancellationToken, nativeArtifact.Size);

                        if (nativeJarDownloaded)
                        {
                            _logger.Information("Extracting natives for {LibraryName} from {NativeJarPath} to {NativesDir}", library.Name, nativeJarLocalPath, nativesDir);
                            ReportLibraryProgress(progress, library.Name, processedLibraries, totalLibraries, $"Extracting native: {nativeClassifierKey}");
                            nativesOk = ExtractNativeJar(nativeJarLocalPath, nativesDir, library.Extract);
                            if (!nativesOk)
                                _logger.Error("Failed to extract natives for library {LibraryName} from {NativeJarPath}", library.Name, nativeJarLocalPath);
                            else
                                _logger.Verbose("Natives for {LibraryName} extracted successfully.", library.Name);
                        }
                        else
                        {
                            _logger.Error("Failed to ensure native JAR for library {LibraryName} ({NativeClassifierKey})", library.Name, nativeClassifierKey);
                            nativesOk = false;
                        }
                    }
                    else
                    {
                        _logger.Warning("Native classifier '{NativeClassifierKey}' specified for OS '{OsName}' in library {LibraryName}, but no corresponding download found in classifiers.", nativeClassifierKey, osName, library.Name);
                    }
                }
                else
                {
                    _logger.Verbose("No specific native classifier for current OS '{OsName}' in library {LibraryName}.", osName, library.Name);
                }
            }

            if (mainArtifactOk && nativesOk)
            {
                successfullyProcessedLibraries++;
                ReportLibraryProgress(progress, library.Name, processedLibraries, totalLibraries, "Processed successfully");
            }
            else
            {
                ReportLibraryProgress(progress, library.Name, processedLibraries, totalLibraries, "Processing failed");
            }
        }

        var applicableLibrariesCount = launchProfile.Libraries.Count(IsLibraryApplicable);
        var allSucceeded = successfullyProcessedLibraries == applicableLibrariesCount;

        if (allSucceeded)
        {
            _logger.Information("All {SuccessfullyProcessedCount} applicable libraries for version {VersionId} processed successfully.", successfullyProcessedLibraries, launchProfile.Id);
        }
        else
        {
            _logger.Error("{FailedCount} out of {ApplicableCount} applicable libraries failed to process for version {VersionId}.",
                applicableLibrariesCount - successfullyProcessedLibraries,
                applicableLibrariesCount, launchProfile.Id);
            return null;
        }

        return classpathEntries;
    }

    private void ReportLibraryProgress(IProgress<LibraryProcessingProgress>? progress, string libraryName, int processed, int total, string status)
    {
        progress?.Report(new LibraryProcessingProgress
        {
            CurrentLibraryName = libraryName,
            ProcessedLibraries = processed,
            TotalLibraries = total,
            Status = status
        });
    }

    // Unchanged methods...
    private bool IsLibraryApplicable(Library library)
    {
        if (library.Rules == null || !library.Rules.Any()) return true;

        var allowed = false;

        foreach (var rule in library.Rules)
        {
            var conditionMet = true;

            if (rule.Os != null)
                if (!CheckOsRule(rule.Os))
                    conditionMet = false;

            if (conditionMet)
            {
                if (rule.Action == RuleAction.Allow)
                    allowed = true;
                else if (rule.Action == RuleAction.Disallow)
                    return false;
            }
        }

        return allowed;
    }

    private bool CheckOsRule(OperatingSystemInfo osRule)
    {
        if (osRule == null) return true;

        var nameMatch = true;
        if (!string.IsNullOrEmpty(osRule.Name))
        {
            var currentOsName = GetCurrentOsNameForNatives();
            nameMatch = osRule.Name.Equals(currentOsName, StringComparison.OrdinalIgnoreCase);
        }

        var archMatch = true;
        if (!string.IsNullOrEmpty(osRule.Arch) && nameMatch)
        {
            var currentArch = OsUtils.GetCurrentArchitecture().ToString().ToLowerInvariant();
            archMatch = osRule.Arch.Equals(currentArch, StringComparison.OrdinalIgnoreCase) || (osRule.Arch == "x86" && currentArch == "x86") || (osRule.Arch == "x64" && currentArch == "x64");
        }

        return nameMatch && archMatch;
    }

    private string GetCurrentOsNameForNatives()
    {
        return OsUtils.GetCurrentOS() switch
        {
            OperatingSystemType.Windows => "windows",
            OperatingSystemType.MacOS => "osx",
            OperatingSystemType.Linux => "linux",
            _ => "unknown"
        };
    }

    private bool ExtractNativeJar(string nativeJarPath, string nativesDir, LibraryExtractRule? extractRule)
    {
        try
        {
            using var archive = ZipFile.OpenRead(nativeJarPath);
            foreach (var entry in archive.Entries)
            {
                if (extractRule?.Exclude != null && extractRule.Exclude.Any(exclusion => entry.FullName.StartsWith(exclusion, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.Verbose("Excluding native entry due to rule: {EntryFullName}", entry.FullName);
                    continue;
                }

                if (string.IsNullOrEmpty(entry.Name)) continue;

                var destinationPath = Path.Combine(nativesDir, entry.Name);
                var entryDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(entryDirectory) && !Directory.Exists(entryDirectory)) Directory.CreateDirectory(entryDirectory);

                _logger.Verbose("Extracting native: {EntryFullName} to {DestinationPath}", entry.FullName, destinationPath);
                entry.ExtractToFile(destinationPath, true);
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

    private async Task<bool> DownloadAndVerifyFileAsync(string url, string localPath, string expectedSha1, string fileDescription, CancellationToken cancellationToken, ulong? expectedSize = null)
    {
        return await _assetManager.DownloadAndVerifyFileAsync(url, localPath, expectedSha1, fileDescription, cancellationToken, expectedSize);
    }

    /// <summary>
    /// Converts a Maven artifact name (groupId:artifactId:version[:classifier]) to a relative path.
    /// e.g. "net.minecraftforge:forge:1.20.1-47.2.0:universal" → "net/minecraftforge/forge/1.20.1-47.2.0/forge-1.20.1-47.2.0-universal.jar"
    /// </summary>
    public static string MavenNameToPath(string name)
    {
        var parts = name.Split(':');
        if (parts.Length < 3) return name;

        var group = parts[0].Replace('.', '/');
        var artifact = parts[1];
        var version = parts[2];
        var classifier = parts.Length >= 4 ? "-" + parts[3] : "";
        var ext = parts.Length >= 5 ? parts[4] : "jar";

        return $"{group}/{artifact}/{version}/{artifact}-{version}{classifier}.{ext}";
    }

    public List<string> ResolveLibraryClasspath(LaunchProfile launchProfile)
    {
        var classpathEntries = new List<string>();
        if (launchProfile.Libraries == null) return classpathEntries;

        foreach (var library in launchProfile.Libraries)
        {
            if (!IsLibraryApplicable(library)) continue;

            if (library.Downloads?.Artifact != null)
            {
                classpathEntries.Add(Path.GetFullPath(Path.Combine(
                    _config.LibrariesDir,
                    library.Downloads.Artifact.Path.Replace('/', Path.DirectorySeparatorChar))));
            }
            else if (!string.IsNullOrEmpty(library.Url) && !string.IsNullOrEmpty(library.Name))
            {
                var mavenPath = MavenNameToPath(library.Name);
                classpathEntries.Add(Path.GetFullPath(Path.Combine(
                    _config.LibrariesDir,
                    mavenPath.Replace('/', Path.DirectorySeparatorChar))));
            }
        }

        return classpathEntries;
    }
}