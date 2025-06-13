using System;
using System.IO;
using Serilog;

namespace ObsidianLauncher;

public class LauncherConfig
{
    public static readonly string VERSION = "1.0-snapshot"; // Version of the launcher

    private readonly ILogger _logger = Log.ForContext<LauncherConfig>(); // Instance logger

    public LauncherConfig(string baseDataDir = ".ObsidianLauncher")
    {
        BaseDataPath = Path.GetFullPath(baseDataDir);
        JavaRuntimesDir = Path.Combine(BaseDataPath, "java_runtimes");
        AssetsDir = Path.Combine(BaseDataPath, "assets");
        AssetObjectsDir = Path.Combine(AssetsDir, "objects");
        AssetIndexesDir = Path.Combine(AssetsDir, "indexes");
        LibrariesDir = Path.Combine(BaseDataPath, "libraries");
        VersionsDir = Path.Combine(BaseDataPath, "versions");
        InstancesRootDir = Path.Combine(BaseDataPath, "instances");
        MojangDownloadsDir = Path.Combine(JavaRuntimesDir, "_downloads", "mojang");
        AdoptiumDownloadsDir = Path.Combine(JavaRuntimesDir, "_downloads", "adoptium");
        DataRootDir = Path.Combine(BaseDataPath);
        LogsDir = Path.Combine(BaseDataPath, "logs");
        InstallerCacheDir = Path.Combine(BaseDataPath, "installerCache");

        EnsureDirectoryExists(BaseDataPath, "Base Data");
        EnsureDirectoryExists(JavaRuntimesDir, "Java Runtimes");
        EnsureDirectoryExists(MojangDownloadsDir, "Mojang Downloads");
        EnsureDirectoryExists(AdoptiumDownloadsDir, "Adoptium Downloads");
        EnsureDirectoryExists(AssetsDir, "Assets");
        EnsureDirectoryExists(AssetObjectsDir, "Asset Objects");
        EnsureDirectoryExists(AssetIndexesDir, "Asset Indexes");
        EnsureDirectoryExists(LibrariesDir, "Libraries");
        EnsureDirectoryExists(VersionsDir, "Versions");
        EnsureDirectoryExists(InstancesRootDir, "Instances Root");
        EnsureDirectoryExists(LogsDir, "Logs");
        EnsureDirectoryExists(InstallerCacheDir, "Installer Cache"); // Ensure the new cache directory exists
    }

    public string BaseDataPath { get; }
    public string JavaRuntimesDir { get; }
    public string AssetsDir { get; }
    public string AssetObjectsDir { get; }
    public string AssetIndexesDir { get; }
    public string LibrariesDir { get; }
    public string VersionsDir { get; }
    public string InstancesRootDir { get; }
    public string MojangDownloadsDir { get; }
    public string AdoptiumDownloadsDir { get; }
    public string LogsDir { get; }
    public string DataRootDir { get; }
    public string InstallerCacheDir { get; } // New Property

    private void EnsureDirectoryExists(string path, string name)
    {
        if (!Directory.Exists(path))
            try
            {
                Directory.CreateDirectory(path);
                _logger.Information("Created {DirectoryName} directory: {Path}", name, path);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create {DirectoryName} directory: {Path}", name, path);
                throw;
            }
    }
}