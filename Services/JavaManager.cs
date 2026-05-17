using System;
using System.Collections.Generic;
using System.Formats.Tar;
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

public class JavaManager
{
    private readonly List<JavaRuntimeInfo> _availableRuntimes;
    private readonly LauncherConfig _config;
    private readonly JavaDownloader _javaDownloader;
    private readonly ILogger _logger;

    public JavaManager(LauncherConfig config, HttpManager httpManager)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = LogHelper.GetLogger<JavaManager>();
        _javaDownloader = new JavaDownloader(httpManager ?? throw new ArgumentNullException(nameof(httpManager)));
        _availableRuntimes = new List<JavaRuntimeInfo>();

        _logger.Verbose("JavaManager initializing...");
        InitializeDirectories();
        ScanForExistingRuntimes();
        _logger.Verbose("JavaManager initialization complete. Found {Count} existing runtimes.", _availableRuntimes.Count);
    }
    
    public async Task<JavaRuntimeInfo?> EnsureJavaForMinecraftVersionAsync(
        LaunchProfile launchProfile,
        CancellationToken cancellationToken = default)
    {
        _logger.Information("Ensuring Java for Minecraft version: {VersionId}", launchProfile.Id);

        var requiredJava = launchProfile.JavaVersion;
        if (requiredJava == null)
        {
            _logger.Error("Launch profile for {VersionId} does not specify a Java version. Cannot proceed.", launchProfile.Id);
            return null;
        }

        _logger.Information("Required Java: Component '{Component}', Major Version '{MajorVersion}'",
            requiredJava.Component, requiredJava.MajorVersion);

        var existingRuntime = _availableRuntimes.FirstOrDefault(r =>
            r.ComponentName.Equals(requiredJava.Component, StringComparison.OrdinalIgnoreCase) &&
            r.MajorVersion == requiredJava.MajorVersion);

        if (existingRuntime != null)
        {
            _logger.Information("Found existing suitable Java runtime: Component '{Component}', Version '{MajorVersion}', Source '{Source}', Home '{HomePath}'",
                existingRuntime.ComponentName, existingRuntime.MajorVersion, existingRuntime.Source, existingRuntime.HomePath);
            return existingRuntime;
        }
        
        // This part remains the same, but it's important to show the full context.
        _logger.Information("No existing suitable Java runtime found for {Component} v{MajorVersion}. Attempting download.", requiredJava.Component, requiredJava.MajorVersion);

        string? downloadedArchivePath = null;
        var sourceApi = "unknown";
        
        _logger.Information("Attempting download from Adoptium for Java {MajorVersion}...", requiredJava.MajorVersion);
        downloadedArchivePath = await _javaDownloader.DownloadJavaForSpecificVersionAdoptiumAsync(requiredJava, _config.AdoptiumDownloadsDir, cancellationToken);
        if (!string.IsNullOrEmpty(downloadedArchivePath))
        {
            sourceApi = "adoptium";
        }
        else
        {
            _logger.Warning(
                "Adoptium download failed or no suitable version found for Java {MajorVersion}. Trying Mojang manifest...",
                requiredJava.MajorVersion);
            downloadedArchivePath = await _javaDownloader.DownloadJavaForJavaVersionMojangAsync(
                requiredJava, _config.MojangDownloadsDir, cancellationToken);
            if (!string.IsNullOrEmpty(downloadedArchivePath)) sourceApi = "mojang";
        }

        if (string.IsNullOrEmpty(downloadedArchivePath))
        {
            _logger.Error("Failed to download Java for component '{Component}' v{MajorVersion} from all sources.", requiredJava.Component, requiredJava.MajorVersion);
            return null;
        }

        _logger.Information("Java archive downloaded via {SourceApi} to: {DownloadedArchivePath}", sourceApi, downloadedArchivePath);

        var extractionTargetDir = GetExtractionPathForRuntime(requiredJava, sourceApi);
        var runtimeNameForPath = Path.GetFileName(extractionTargetDir);

        if (ExtractJavaArchive(downloadedArchivePath, extractionTargetDir, runtimeNameForPath))
        {
            _logger.Information("Java archive extracted to: {ExtractionTargetDir}", extractionTargetDir);
            var javaExePath = FindJavaExecutable(extractionTargetDir);

            if (!string.IsNullOrEmpty(javaExePath))
            {
                var effectiveJavaHome = Path.GetDirectoryName(Path.GetDirectoryName(javaExePath))!;
                var newRuntime = new JavaRuntimeInfo
                {
                    HomePath = effectiveJavaHome,
                    JavaExecutablePath = javaExePath,
                    MajorVersion = requiredJava.MajorVersion,
                    ComponentName = requiredJava.Component,
                    Source = sourceApi
                };
                _availableRuntimes.Add(newRuntime);

                _logger.Information("Successfully configured Java runtime: Component={Component}, Version={MajorVersion}, Source={Source}, Home='{HomePath}', Executable='{JavaExecutablePath}'",
                    newRuntime.ComponentName, newRuntime.MajorVersion, newRuntime.Source, newRuntime.HomePath, newRuntime.JavaExecutablePath);

                try
                {
                    File.Delete(downloadedArchivePath);
                    _logger.Information("Removed downloaded archive: {DownloadedArchivePath}", downloadedArchivePath);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to remove downloaded archive {DownloadedArchivePath}", downloadedArchivePath);
                }

                return newRuntime;
            }
            _logger.Error("Failed to find Java executable in the extracted archive at {ExtractionTargetDir}. Possible extraction issue or unexpected archive structure.", extractionTargetDir);
        }
        else
        {
            _logger.Error("Failed to extract Java archive {DownloadedArchivePath} to {ExtractionTargetDir}", downloadedArchivePath, extractionTargetDir);
        }
        
        if (File.Exists(downloadedArchivePath))
            try
            {
                File.Delete(downloadedArchivePath);
                _logger.Information("Cleaned up downloaded archive after failure: {DownloadedArchivePath}", downloadedArchivePath);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Cleanup: Failed to remove archive {DownloadedArchivePath} after failure", downloadedArchivePath);
            }
        return null;
    }
    

    private void InitializeDirectories()
    {
        if (!Directory.Exists(_config.JavaRuntimesDir))
        {
            _logger.Information("Java runtimes directory {JavaRuntimesDir} does not exist. Creating.",
                _config.JavaRuntimesDir);
            try
            {
                Directory.CreateDirectory(_config.JavaRuntimesDir);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create Java runtimes directory: {JavaRuntimesDir}",
                    _config.JavaRuntimesDir);
                // Potentially throw or handle this critical failure
            }
        }

        // Ensure _downloads subdirectories also exist for the downloader
        Directory.CreateDirectory(_config.MojangDownloadsDir);
        Directory.CreateDirectory(_config.AdoptiumDownloadsDir);
    }

    /// <summary>
    ///     Extracts a Java archive (ZIP or TAR.GZ) to the specified directory.
    /// </summary>
    /// <param name="archivePath">Path to the Java archive file.</param>
    /// <param name="extractionDir">Directory where the archive should be extracted.</param>
    /// <param name="runtimeNameForPath">A descriptive name for logging, usually derived from component and version.</param>
    /// <returns>True if extraction was successful, false otherwise.</returns>
    public bool ExtractJavaArchive(string archivePath, string extractionDir, string runtimeNameForPath)
    {
        _logger.Information("Attempting to extract Java archive '{RuntimeName}': {ArchivePath} to {ExtractionDir}",
            runtimeNameForPath, archivePath, extractionDir);

        try
        {
            if (Directory.Exists(extractionDir))
            {
                _logger.Information(
                    "Extraction directory {ExtractionDir} for '{RuntimeName}' already exists. Removing for fresh extraction.",
                    extractionDir, runtimeNameForPath);
                Directory.Delete(extractionDir, true); // Recursive delete
            }

            Directory.CreateDirectory(extractionDir);

            // System.IO.Compression.ZipFile handles .zip archives.
            // For .tar.gz, you'd need an external library like SharpZipLib or System.Formats.Tar (in .NET 7+)
            // For simplicity, this example assumes .zip. If .tar.gz is common, this needs expansion.
            if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archivePath, extractionDir, true);
                _logger.Information("Successfully extracted ZIP archive '{RuntimeName}' to {ExtractionDir}.",
                    runtimeNameForPath, extractionDir);
                return true;
            }

            if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                using var fileStream = File.OpenRead(archivePath);
                using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                TarFile.ExtractToDirectory(gzipStream, extractionDir, overwriteFiles: true);
                _logger.Information("Successfully extracted TAR.GZ archive '{RuntimeName}' to {ExtractionDir}.",
                    runtimeNameForPath, extractionDir);

                // On Unix, TarFile.ExtractToDirectory does not restore executable bits.
                // Set the execute bit on all files in bin/ directories.
                if (!OperatingSystem.IsWindows())
                    SetExecutableBitsInBinDirs(extractionDir);

                return true;
            }

            _logger.Error(
                "Unsupported archive format for '{RuntimeName}': {ArchivePath}. Only .zip and .tar.gz are supported.",
                runtimeNameForPath, archivePath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to extract Java archive '{RuntimeName}' ({ArchivePath}) to {ExtractionDir}.",
                runtimeNameForPath, archivePath, extractionDir);
            // Attempt to clean up partially extracted directory
            if (Directory.Exists(extractionDir))
                try
                {
                    Directory.Delete(extractionDir, true);
                    _logger.Warning("Cleaned up extraction directory {ExtractionDir} for '{RuntimeName}' after error.",
                        extractionDir, runtimeNameForPath);
                }
                catch (Exception delEx)
                {
                    _logger.Error(delEx,
                        "Failed to cleanup extraction directory {ExtractionDir} for '{RuntimeName}' after error.",
                        extractionDir, runtimeNameForPath);
                }

            return false;
        }
    }

    /// <summary>
    ///     Finds the Java executable (java or javaw) within an extracted Java runtime directory.
    ///     Accounts for common Java directory structures (e.g., JDK vs JRE, macOS bundle).
    /// </summary>
    /// <param name="extractedJavaBaseDir">The base directory where the Java archive was extracted.</param>
    /// <returns>The full path to the Java executable, or null if not found.</returns>
    public string? FindJavaExecutable(string extractedJavaBaseDir)
    {
        _logger.Verbose("Attempting to find Java executable in/under: {ExtractionBaseDir}", extractedJavaBaseDir);

        if (!Directory.Exists(extractedJavaBaseDir))
        {
            _logger.Error("Java base directory {ExtractionBaseDir} does not exist or is not a directory.",
                extractedJavaBaseDir);
            return null;
        }

        // Common Java structures:
        // 1. <extractedJavaBaseDir>/bin/java(.exe)  (Typical for many JDK/JRE zip/tar.gz)
        // 2. <extractedJavaBaseDir>/<jdk-root-dir>/bin/java(.exe) (e.g. jdk-17.0.1/bin/java)
        // 3. macOS: <extractedJavaBaseDir>/<jre-bundle-name>.jre/Contents/Home/bin/java

        var searchPaths = new List<string>();
        var javaExeName = OsUtils.GetCurrentOS() == OperatingSystemType.Windows ? "javaw.exe" : "java";
        var alternativeJavaExeName = OsUtils.GetCurrentOS() == OperatingSystemType.Windows ? "java.exe" : null;


        // Path 1: Directly in <base>/bin
        searchPaths.Add(Path.Combine(extractedJavaBaseDir, "bin", javaExeName));
        if (alternativeJavaExeName != null)
            searchPaths.Add(Path.Combine(extractedJavaBaseDir, "bin", alternativeJavaExeName));


        // Path 3: macOS specific (check before general subdirectory scan for clarity)
        if (OsUtils.GetCurrentOS() == OperatingSystemType.MacOS)
        {
            // Look for a .jre or .jdk bundle first inside the extractedJavaBaseDir
            var bundleDirs = Directory.GetDirectories(extractedJavaBaseDir)
                .Where(d => d.EndsWith(".jre", StringComparison.OrdinalIgnoreCase) ||
                            d.EndsWith(".jdk", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (bundleDirs.Any())
                foreach (var bundleDir in bundleDirs)
                    searchPaths.Add(Path.Combine(bundleDir, "Contents", "Home", "bin", javaExeName));
            else
                // If no .jre/.jdk bundle, it might be a more direct structure as in Adoptium macOS tar.gz
                // (e.g. Contents/Home/bin directly under the root folder that has the version name)
                searchPaths.Add(Path.Combine(extractedJavaBaseDir, "Contents", "Home", "bin", javaExeName));
        }

        // Path 2: In a single nested root directory like <base>/jdk-17.0.1/bin
        // This is common if the archive itself contains a single top-level folder.
        var subDirs = Directory.GetDirectories(extractedJavaBaseDir).ToList();
        if (subDirs.Count == 1) // If there's only ONE subdirectory, assume it's the root of the JDK/JRE
        {
            var nestedJavaHome = subDirs[0];
            searchPaths.Add(Path.Combine(nestedJavaHome, "bin", javaExeName));
            if (alternativeJavaExeName != null)
                searchPaths.Add(Path.Combine(nestedJavaHome, "bin", alternativeJavaExeName));

            if (OsUtils.GetCurrentOS() ==
                OperatingSystemType.MacOS) // Also check macOS structure within this nested dir
            {
                var nestedBundleDirs = Directory.GetDirectories(nestedJavaHome)
                    .Where(d => d.EndsWith(".jre", StringComparison.OrdinalIgnoreCase) ||
                                d.EndsWith(".jdk", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (nestedBundleDirs.Any())
                    foreach (var bundleDir in nestedBundleDirs)
                        searchPaths.Add(Path.Combine(bundleDir, "Contents", "Home", "bin", javaExeName));
                else
                    searchPaths.Add(Path.Combine(nestedJavaHome, "Contents", "Home", "bin", javaExeName));
            }
        }
        else if (subDirs.Count > 1)
        {
            _logger.Verbose("Multiple subdirectories found in {ExtractionBaseDir}. Will check common ones.",
                extractedJavaBaseDir);
            // If multiple subdirs, we could try to be smarter, e.g., look for a 'release' file
            // or a dir name that matches the expected Java version.
            // For now, we'll rely on the direct paths and the macOS bundle check if applicable.
            // One could iterate subDirs and add `Path.Combine(subDir, "bin", javaExeName)` for each.
            foreach (var subDir in subDirs)
                // Prioritize dirs that look like JDK/JRE homes
                if (Path.GetFileName(subDir).StartsWith("jdk", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(subDir).StartsWith("jre", StringComparison.OrdinalIgnoreCase) ||
                    File.Exists(Path.Combine(subDir, "release"))) // 'release' file is a good indicator
                {
                    searchPaths.Add(Path.Combine(subDir, "bin", javaExeName));
                    if (alternativeJavaExeName != null)
                        searchPaths.Add(Path.Combine(subDir, "bin", alternativeJavaExeName));
                    if (OsUtils.GetCurrentOS() == OperatingSystemType.MacOS)
                        searchPaths.Add(Path.Combine(subDir, "Contents", "Home", "bin", javaExeName));
                }
        }


        foreach (var path in searchPaths.Distinct()) // Distinct to avoid redundant checks
        {
            _logger.Verbose("Checking for Java executable at: {Path}", path);
            if (File.Exists(path))
            {
                _logger.Information("Found Java executable: {JavaExePath}", path);
                return path;
            }
        }

        _logger.Error(
            "Java executable ('{JavaExeName}' or '{AlternativeJavaExeName}') not found in any expected locations within {ExtractionBaseDir}",
            javaExeName, alternativeJavaExeName ?? "N/A", extractedJavaBaseDir);
        return null;
    }

    /// <summary>
    ///     Scans the configured Java runtimes directory for existing, valid Java installations.
    ///     Populates the internal list of available runtimes.
    /// </summary>
    public void ScanForExistingRuntimes()
    {
        _availableRuntimes.Clear();
        if (!Directory.Exists(_config.JavaRuntimesDir))
        {
            _logger.Warning(
                "Java runtimes directory {JavaRuntimesDir} does not exist. Cannot scan for existing runtimes.",
                _config.JavaRuntimesDir);
            return;
        }

        _logger.Information("Scanning for existing Java runtimes in {JavaRuntimesDir}...", _config.JavaRuntimesDir);
        foreach (var dirPath in Directory.EnumerateDirectories(_config.JavaRuntimesDir))
        {
            var dirName = Path.GetFileName(dirPath);
            // Skip special directories like "_downloads"
            if (dirName.StartsWith("_"))
            {
                _logger.Verbose("Skipping special directory: {Directory}", dirPath);
                continue;
            }

            _logger.Verbose("Scanning potential Java runtime directory: {DirectoryPath}", dirPath);
            var javaExePath = FindJavaExecutable(dirPath);

            if (!string.IsNullOrEmpty(javaExePath))
            {
                // Try to parse component and version from directory name (e.g., "jre-legacy_17" or "adoptium_jdk-hotspot_17")
                var source = "unknown_source"; // Default if not part of dir name
                var component = "unknown_component";
                uint majorVersion = 0;

                var nameParts = dirName.Split('_');
                if (nameParts.Length >= 2) // Must have at least component_version
                {
                    if (uint.TryParse(nameParts.Last(), out var parsedVersion))
                    {
                        majorVersion = parsedVersion;
                        if (nameParts.Length == 2) // component_version
                        {
                            component = nameParts[0];
                            source = "user_provided"; // Or some other default if source isn't in the name
                        }
                        else // source_component_version or source_subcomp1_subcomp2_version
                        {
                            source = nameParts[0];
                            component = string.Join("_", nameParts.Skip(1).Take(nameParts.Length - 2));
                        }
                    }
                    else
                    {
                        _logger.Warning(
                            "Could not parse major version from directory name suffix '{Suffix}' of '{DirName}'",
                            nameParts.Last(), dirName);
                    }
                }
                else
                {
                    _logger.Warning(
                        "Directory name '{DirName}' does not follow expected '[source_]component_version' format. Cannot reliably determine Java details.",
                        dirName);
                }


                if (majorVersion > 0 && component != "unknown_component" && !string.IsNullOrEmpty(component))
                {
                    var effectiveJavaHome = Path.GetDirectoryName(Path.GetDirectoryName(javaExePath))!; // Up from /bin
                    var runtimeInfo = new JavaRuntimeInfo
                    {
                        HomePath = effectiveJavaHome,
                        JavaExecutablePath = javaExePath,
                        MajorVersion = majorVersion,
                        ComponentName = component,
                        Source = source
                    };
                    _availableRuntimes.Add(runtimeInfo);
                    _logger.Information(
                        "Discovered existing runtime: Component='{Component}', Version='{MajorVersion}', Source='{Source}', Home='{HomePath}', Exe='{JavaExePath}'",
                        component, majorVersion, source, effectiveJavaHome, javaExePath);
                }
                else
                {
                    _logger.Warning(
                        "Found Java executable in {DirectoryPath} but could not determine full component/version details from directory name '{DirName}'. Skipping this runtime.",
                        dirPath, dirName);
                }
            }
            else
            {
                _logger.Verbose("No Java executable found in candidate directory: {DirectoryPath}", dirPath);
            }
        }

        _logger.Information("Java runtime scan complete. Found {Count} usable existing runtimes.",
            _availableRuntimes.Count);
    }

    /// <summary>
    ///     Gets a list of currently known available Java runtimes.
    /// </summary>
    public List<JavaRuntimeInfo> GetAvailableRuntimes()
    {
        return new List<JavaRuntimeInfo>(_availableRuntimes);
        // Return a copy
    }

    private string GetExtractionPathForRuntime(JavaVersionInfo javaVersion, string sourceApi)
    {
        // Example: jre-legacy_17 or adoptium_jdk-hotspot_17
        var dirName = $"{sourceApi}_{javaVersion.Component}_{javaVersion.MajorVersion}";
        return Path.Combine(_config.JavaRuntimesDir, dirName);
    }

    /// <summary>
    /// Sets the executable bit on all files inside bin/ subdirectories recursively.
    /// Required on Linux/macOS after TAR.GZ extraction since .NET's TarFile does not
    /// preserve Unix file permissions.
    /// </summary>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private void SetExecutableBitsInBinDirs(string rootDir)
    {
        try
        {
            foreach (var binDir in Directory.EnumerateDirectories(rootDir, "bin", SearchOption.AllDirectories))
            {
                foreach (var file in Directory.EnumerateFiles(binDir))
                {
                    try
                    {
                        var current = File.GetUnixFileMode(file);
                        var withExec = current
                            | UnixFileMode.UserExecute
                            | UnixFileMode.GroupExecute
                            | UnixFileMode.OtherExecute;
                        File.SetUnixFileMode(file, withExec);
                        _logger.Verbose("Set executable bit on: {File}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to set executable bit on {File}", file);
                    }
                }
            }

            // Also handle lib/jspawnhelper and similar helper binaries
            foreach (var libDir in Directory.EnumerateDirectories(rootDir, "lib", SearchOption.AllDirectories))
            {
                foreach (var file in Directory.EnumerateFiles(libDir, "jspawnhelper", SearchOption.AllDirectories))
                {
                    try
                    {
                        var current = File.GetUnixFileMode(file);
                        File.SetUnixFileMode(file, current | UnixFileMode.UserExecute | UnixFileMode.GroupExecute);
                    }
                    catch { /* best-effort */ }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error setting executable bits in {RootDir}", rootDir);
        }
    }
}