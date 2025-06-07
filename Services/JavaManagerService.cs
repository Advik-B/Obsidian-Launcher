using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression; // For ZipFile
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;    // For MinecraftVersion, JavaVersionInfo, JavaRuntimeInfo
using ObsidianLauncher.Utils;      // For OsUtils, LoggerSetup (though logger is injected)
using ObsidianLauncher.Enums;      // For OperatingSystemType, ArchitectureType
using Serilog;

namespace ObsidianLauncher.Services
{
    public class JavaManagerService
    {
        private readonly LauncherConfig _config;
        // HttpManagerService is now owned by JavaDownloaderService
        private readonly JavaDownloaderService _javaDownloader;
        private readonly ILogger _logger;
        private List<JavaRuntimeInfo> _availableRuntimes;

        public JavaManagerService(LauncherConfig config, HttpManagerService httpManager)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = Log.ForContext<JavaManagerService>();
            // JavaDownloaderService now takes HttpManagerService
            _javaDownloader = new JavaDownloaderService(httpManager ?? throw new ArgumentNullException(nameof(httpManager)));
            _availableRuntimes = new List<JavaRuntimeInfo>();

            _logger.Verbose("JavaManagerService initializing...");
            InitializeDirectories();
            ScanForExistingRuntimes();
            _logger.Verbose("JavaManagerService initialization complete. Found {Count} existing runtimes.", _availableRuntimes.Count);
        }

        private void InitializeDirectories()
        {
            if (!Directory.Exists(_config.JavaRuntimesDir))
            {
                _logger.Information("Java runtimes directory {JavaRuntimesDir} does not exist. Creating.", _config.JavaRuntimesDir);
                try
                {
                    Directory.CreateDirectory(_config.JavaRuntimesDir);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to create Java runtimes directory: {JavaRuntimesDir}", _config.JavaRuntimesDir);
                    // Potentially throw or handle this critical failure
                }
            }
            // Ensure _downloads subdirectories also exist for the downloader
            Directory.CreateDirectory(_config.MojangDownloadsDir);
            Directory.CreateDirectory(_config.AdoptiumDownloadsDir);
        }

        /// <summary>
        /// Ensures a suitable Java runtime is available for the given Minecraft version.
        /// It first checks existing runtimes, then attempts to download and extract if necessary.
        /// </summary>
        /// <param name="mcVersion">The Minecraft version details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Information about the ensured Java runtime, or null if unsuccessful.</returns>
        public async Task<JavaRuntimeInfo> EnsureJavaForMinecraftVersionAsync(
            MinecraftVersion mcVersion,
            CancellationToken cancellationToken = default)
        {
            _logger.Information("Ensuring Java for Minecraft version: {VersionId}", mcVersion.Id);

            var requiredJava = mcVersion.JavaVersion;
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

            _logger.Information("No existing suitable Java runtime found for {Component} v{MajorVersion}. Attempting download.",
                requiredJava.Component, requiredJava.MajorVersion);

            string downloadedArchivePath = null;
            string sourceApi = "unknown";

            // Try Adoptium first as it's generally preferred for broader Java versions
            _logger.Information("Attempting download from Adoptium for Java {MajorVersion}...", requiredJava.MajorVersion);
            downloadedArchivePath = await _javaDownloader.DownloadJavaForSpecificVersionAdoptiumAsync(requiredJava, _config.AdoptiumDownloadsDir, cancellationToken);
            if (!string.IsNullOrEmpty(downloadedArchivePath))
            {
                sourceApi = "adoptium";
            }
            else
            {
                _logger.Warning("Adoptium download failed or no suitable version found for Java {MajorVersion}. Trying Mojang manifest...", requiredJava.MajorVersion);
                downloadedArchivePath = await _javaDownloader.DownloadJavaForMinecraftVersionMojangAsync(mcVersion, _config.MojangDownloadsDir, cancellationToken);
                if (!string.IsNullOrEmpty(downloadedArchivePath))
                {
                    sourceApi = "mojang";
                }
            }

            if (string.IsNullOrEmpty(downloadedArchivePath))
            {
                _logger.Error("Failed to download Java for component '{Component}' v{MajorVersion} from all sources.",
                    requiredJava.Component, requiredJava.MajorVersion);
                return null;
            }

            _logger.Information("Java archive downloaded via {SourceApi} to: {DownloadedArchivePath}", sourceApi, downloadedArchivePath);

            // Determine extraction path based on component and version to keep things organized
            // Example: .mylauncher_data/java_runtimes/jre-legacy_17
            string extractionTargetDir = GetExtractionPathForRuntime(requiredJava, sourceApi);
            string runtimeNameForPath = Path.GetFileName(extractionTargetDir); // Used for logging/display

            if (ExtractJavaArchive(downloadedArchivePath, extractionTargetDir, runtimeNameForPath))
            {
                _logger.Information("Java archive extracted to: {ExtractionTargetDir}", extractionTargetDir);
                string javaExePath = FindJavaExecutable(extractionTargetDir);

                if (!string.IsNullOrEmpty(javaExePath))
                {
                    // The "home" path is typically the directory containing the "bin" directory
                    string effectiveJavaHome = Path.GetDirectoryName(Path.GetDirectoryName(javaExePath));

                    var newRuntime = new JavaRuntimeInfo
                    {
                        HomePath = effectiveJavaHome,
                        JavaExecutablePath = javaExePath,
                        MajorVersion = requiredJava.MajorVersion,
                        ComponentName = requiredJava.Component,
                        Source = sourceApi // Store where it came from
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
                else
                {
                    _logger.Error("Failed to find Java executable in the extracted archive at {ExtractionTargetDir}. Possible extraction issue or unexpected archive structure.", extractionTargetDir);
                }
            }
            else
            {
                _logger.Error("Failed to extract Java archive {DownloadedArchivePath} to {ExtractionTargetDir}", downloadedArchivePath, extractionTargetDir);
            }

            // Cleanup downloaded archive if extraction or finding executable failed
            if (File.Exists(downloadedArchivePath))
            {
                try
                {
                    File.Delete(downloadedArchivePath);
                    _logger.Information("Cleaned up downloaded archive after failure: {DownloadedArchivePath}", downloadedArchivePath);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Cleanup: Failed to remove archive {DownloadedArchivePath} after failure", downloadedArchivePath);
                }
            }
            return null;
        }


        /// <summary>
        /// Extracts a Java archive (ZIP or TAR.GZ) to the specified directory.
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
                    _logger.Information("Extraction directory {ExtractionDir} for '{RuntimeName}' already exists. Removing for fresh extraction.",
                        extractionDir, runtimeNameForPath);
                    Directory.Delete(extractionDir, true); // Recursive delete
                }
                Directory.CreateDirectory(extractionDir);

                // System.IO.Compression.ZipFile handles .zip archives.
                // For .tar.gz, you'd need an external library like SharpZipLib or System.Formats.Tar (in .NET 7+)
                // For simplicity, this example assumes .zip. If .tar.gz is common, this needs expansion.
                if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    ZipFile.ExtractToDirectory(archivePath, extractionDir, true); // true to overwrite files
                    _logger.Information("Successfully extracted ZIP archive '{RuntimeName}' to {ExtractionDir}.",
                        runtimeNameForPath, extractionDir);
                    return true;
                }
                // else if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                // {
                //     _logger.Information("Attempting to extract TAR.GZ archive '{RuntimeName}' to {ExtractionDir}...",
                //        runtimeNameForPath, extractionDir);
                //     // Implement TAR.GZ extraction here using SharpZipLib or System.Formats.Tar
                //     // For example with System.Formats.Tar (requires .NET 7+):
                //     // using var fileStream = File.OpenRead(archivePath);
                //     // using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                //     // TarFile.ExtractToDirectory(gzipStream, extractionDir, true);
                //     _logger.Error("TAR.GZ extraction not yet implemented for '{RuntimeName}'.", runtimeNameForPath);
                //     return false;
                // }
                else
                {
                    _logger.Error("Unsupported archive format for '{RuntimeName}': {ArchivePath}. Only .zip is currently supported.",
                        runtimeNameForPath, archivePath);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to extract Java archive '{RuntimeName}' ({ArchivePath}) to {ExtractionDir}.",
                    runtimeNameForPath, archivePath, extractionDir);
                // Attempt to clean up partially extracted directory
                if (Directory.Exists(extractionDir))
                {
                    try { Directory.Delete(extractionDir, true); _logger.Warning("Cleaned up extraction directory {ExtractionDir} for '{RuntimeName}' after error.", extractionDir, runtimeNameForPath); }
                    catch (Exception delEx) { _logger.Error(delEx, "Failed to cleanup extraction directory {ExtractionDir} for '{RuntimeName}' after error.", extractionDir, runtimeNameForPath); }
                }
                return false;
            }
        }

        /// <summary>
        /// Finds the Java executable (java or javaw) within an extracted Java runtime directory.
        /// Accounts for common Java directory structures (e.g., JDK vs JRE, macOS bundle).
        /// </summary>
        /// <param name="extractedJavaBaseDir">The base directory where the Java archive was extracted.</param>
        /// <returns>The full path to the Java executable, or null if not found.</returns>
        public string FindJavaExecutable(string extractedJavaBaseDir)
        {
            _logger.Verbose("Attempting to find Java executable in/under: {ExtractionBaseDir}", extractedJavaBaseDir);

            if (!Directory.Exists(extractedJavaBaseDir))
            {
                _logger.Error("Java base directory {ExtractionBaseDir} does not exist or is not a directory.", extractedJavaBaseDir);
                return null;
            }

            // Common Java structures:
            // 1. <extractedJavaBaseDir>/bin/java(.exe)  (Typical for many JDK/JRE zip/tar.gz)
            // 2. <extractedJavaBaseDir>/<jdk-root-dir>/bin/java(.exe) (e.g. jdk-17.0.1/bin/java)
            // 3. macOS: <extractedJavaBaseDir>/<jre-bundle-name>.jre/Contents/Home/bin/java

            List<string> searchPaths = new List<string>();
            string javaExeName = OsUtils.GetCurrentOS() == OperatingSystemType.Windows ? "javaw.exe" : "java";
            string alternativeJavaExeName = OsUtils.GetCurrentOS() == OperatingSystemType.Windows ? "java.exe" : null;


            // Path 1: Directly in <base>/bin
            searchPaths.Add(Path.Combine(extractedJavaBaseDir, "bin", javaExeName));
            if (alternativeJavaExeName != null) searchPaths.Add(Path.Combine(extractedJavaBaseDir, "bin", alternativeJavaExeName));


            // Path 3: macOS specific (check before general subdirectory scan for clarity)
            if (OsUtils.GetCurrentOS() == OperatingSystemType.MacOS)
            {
                // Look for a .jre or .jdk bundle first inside the extractedJavaBaseDir
                var bundleDirs = Directory.GetDirectories(extractedJavaBaseDir)
                                          .Where(d => d.EndsWith(".jre", StringComparison.OrdinalIgnoreCase) ||
                                                      d.EndsWith(".jdk", StringComparison.OrdinalIgnoreCase))
                                          .ToList();
                if (bundleDirs.Any()) {
                     foreach (var bundleDir in bundleDirs) {
                        searchPaths.Add(Path.Combine(bundleDir, "Contents", "Home", "bin", javaExeName));
                     }
                } else {
                    // If no .jre/.jdk bundle, it might be a more direct structure as in Adoptium macOS tar.gz
                    // (e.g. Contents/Home/bin directly under the root folder that has the version name)
                    searchPaths.Add(Path.Combine(extractedJavaBaseDir, "Contents", "Home", "bin", javaExeName));
                }
            }

            // Path 2: In a single nested root directory like <base>/jdk-17.0.1/bin
            // This is common if the archive itself contains a single top-level folder.
            var subDirs = Directory.GetDirectories(extractedJavaBaseDir).ToList();
            if (subDirs.Count == 1) // If there's only ONE subdirectory, assume it's the root of the JDK/JRE
            {
                string nestedJavaHome = subDirs[0];
                searchPaths.Add(Path.Combine(nestedJavaHome, "bin", javaExeName));
                if (alternativeJavaExeName != null) searchPaths.Add(Path.Combine(nestedJavaHome, "bin", alternativeJavaExeName));

                if (OsUtils.GetCurrentOS() == OperatingSystemType.MacOS) // Also check macOS structure within this nested dir
                {
                     var nestedBundleDirs = Directory.GetDirectories(nestedJavaHome)
                                          .Where(d => d.EndsWith(".jre", StringComparison.OrdinalIgnoreCase) ||
                                                      d.EndsWith(".jdk", StringComparison.OrdinalIgnoreCase))
                                          .ToList();
                    if (nestedBundleDirs.Any()) {
                        foreach (var bundleDir in nestedBundleDirs) {
                            searchPaths.Add(Path.Combine(bundleDir, "Contents", "Home", "bin", javaExeName));
                        }
                    } else {
                         searchPaths.Add(Path.Combine(nestedJavaHome, "Contents", "Home", "bin", javaExeName));
                    }
                }
            }
            else if (subDirs.Count > 1)
            {
                _logger.Verbose("Multiple subdirectories found in {ExtractionBaseDir}. Will check common ones.", extractedJavaBaseDir);
                // If multiple subdirs, we could try to be smarter, e.g., look for a 'release' file
                // or a dir name that matches the expected Java version.
                // For now, we'll rely on the direct paths and the macOS bundle check if applicable.
                // One could iterate subDirs and add `Path.Combine(subDir, "bin", javaExeName)` for each.
                foreach(var subDir in subDirs)
                {
                    // Prioritize dirs that look like JDK/JRE homes
                    if(Path.GetFileName(subDir).StartsWith("jdk", StringComparison.OrdinalIgnoreCase) ||
                       Path.GetFileName(subDir).StartsWith("jre", StringComparison.OrdinalIgnoreCase) ||
                       File.Exists(Path.Combine(subDir, "release"))) // 'release' file is a good indicator
                    {
                        searchPaths.Add(Path.Combine(subDir, "bin", javaExeName));
                        if (alternativeJavaExeName != null) searchPaths.Add(Path.Combine(subDir, "bin", alternativeJavaExeName));
                         if (OsUtils.GetCurrentOS() == OperatingSystemType.MacOS)
                         {
                             searchPaths.Add(Path.Combine(subDir, "Contents", "Home", "bin", javaExeName));
                         }
                    }
                }
            }


            foreach (string path in searchPaths.Distinct()) // Distinct to avoid redundant checks
            {
                _logger.Verbose("Checking for Java executable at: {Path}", path);
                if (File.Exists(path))
                {
                    _logger.Information("Found Java executable: {JavaExePath}", path);
                    return path;
                }
            }

            _logger.Error("Java executable ('{JavaExeName}' or '{AlternativeJavaExeName}') not found in any expected locations within {ExtractionBaseDir}",
                javaExeName, alternativeJavaExeName ?? "N/A", extractedJavaBaseDir);
            return null;
        }

        /// <summary>
        /// Scans the configured Java runtimes directory for existing, valid Java installations.
        /// Populates the internal list of available runtimes.
        /// </summary>
        public void ScanForExistingRuntimes()
        {
            _availableRuntimes.Clear();
            if (!Directory.Exists(_config.JavaRuntimesDir))
            {
                _logger.Warning("Java runtimes directory {JavaRuntimesDir} does not exist. Cannot scan for existing runtimes.", _config.JavaRuntimesDir);
                return;
            }

            _logger.Information("Scanning for existing Java runtimes in {JavaRuntimesDir}...", _config.JavaRuntimesDir);
            foreach (string dirPath in Directory.EnumerateDirectories(_config.JavaRuntimesDir))
            {
                string dirName = Path.GetFileName(dirPath);
                // Skip special directories like "_downloads"
                if (dirName.StartsWith("_"))
                {
                    _logger.Verbose("Skipping special directory: {Directory}", dirPath);
                    continue;
                }

                _logger.Verbose("Scanning potential Java runtime directory: {DirectoryPath}", dirPath);
                string javaExePath = FindJavaExecutable(dirPath);

                if (!string.IsNullOrEmpty(javaExePath))
                {
                    // Try to parse component and version from directory name (e.g., "jre-legacy_17" or "adoptium_jdk-hotspot_17")
                    string source = "unknown_source"; // Default if not part of dir name
                    string component = "unknown_component";
                    uint majorVersion = 0;

                    var nameParts = dirName.Split('_');
                    if (nameParts.Length >= 2) // Must have at least component_version
                    {
                        if (uint.TryParse(nameParts.Last(), out uint parsedVersion))
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
                            _logger.Warning("Could not parse major version from directory name suffix '{Suffix}' of '{DirName}'", nameParts.Last(), dirName);
                        }
                    }
                    else
                    {
                        _logger.Warning("Directory name '{DirName}' does not follow expected '[source_]component_version' format. Cannot reliably determine Java details.", dirName);
                    }


                    if (majorVersion > 0 && component != "unknown_component" && !string.IsNullOrEmpty(component))
                    {
                        string effectiveJavaHome = Path.GetDirectoryName(Path.GetDirectoryName(javaExePath)); // Up from /bin
                        var runtimeInfo = new JavaRuntimeInfo
                        {
                            HomePath = effectiveJavaHome,
                            JavaExecutablePath = javaExePath,
                            MajorVersion = majorVersion,
                            ComponentName = component,
                            Source = source
                        };
                        _availableRuntimes.Add(runtimeInfo);
                        _logger.Information("Discovered existing runtime: Component='{Component}', Version='{MajorVersion}', Source='{Source}', Home='{HomePath}', Exe='{JavaExePath}'",
                            component, majorVersion, source, effectiveJavaHome, javaExePath);
                    }
                    else
                    {
                        _logger.Warning("Found Java executable in {DirectoryPath} but could not determine full component/version details from directory name '{DirName}'. Skipping this runtime.",
                            dirPath, dirName);
                    }
                }
                else
                {
                    _logger.Verbose("No Java executable found in candidate directory: {DirectoryPath}", dirPath);
                }
            }
            _logger.Information("Java runtime scan complete. Found {Count} usable existing runtimes.", _availableRuntimes.Count);
        }

        /// <summary>
        /// Gets a list of currently known available Java runtimes.
        /// </summary>
        public List<JavaRuntimeInfo> GetAvailableRuntimes() => new List<JavaRuntimeInfo>(_availableRuntimes); // Return a copy

        private string GetExtractionPathForRuntime(JavaVersionInfo javaVersion, string sourceApi)
        {
            // Example: jre-legacy_17 or adoptium_jdk-hotspot_17
            string dirName = $"{sourceApi}_{javaVersion.Component}_{javaVersion.MajorVersion}";
            return Path.Combine(_config.JavaRuntimesDir, dirName);
        }
    }
}