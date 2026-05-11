// Services/ResourceManager.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services;

/// <summary>
///     Manages resource folders (mods, resource packs, shaders, worlds, screenshots) for instances.
/// </summary>
public class ResourceManager
{
    private readonly ILogger _logger;
    private const string MetadataFileName = ".resource_metadata.json";

    public ResourceManager()
    {
        _logger = LogHelper.GetLogger<ResourceManager>();
    }

    /// <summary>
    ///     Gets the folder path for a specific resource type in an instance.
    /// </summary>
    public string GetResourceFolderPath(Instance instance, ResourceFolderType folderType)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        var folderName = folderType switch
        {
            ResourceFolderType.Mods => "mods",
            ResourceFolderType.ResourcePacks => "resourcepacks",
            ResourceFolderType.ShaderPacks => "shaderpacks",
            ResourceFolderType.TexturePacks => "texturepacks",
            ResourceFolderType.DataPacks => Path.Combine("saves", "datapacks"),
            ResourceFolderType.Worlds => "saves",
            ResourceFolderType.Screenshots => "screenshots",
            _ => throw new ArgumentException($"Unknown resource folder type: {folderType}", nameof(folderType))
        };

        return Path.Combine(instance.InstancePath, folderName);
    }

    /// <summary>
    ///     Ensures a resource folder exists for an instance.
    /// </summary>
    public void EnsureResourceFolder(Instance instance, ResourceFolderType folderType)
    {
        var folderPath = GetResourceFolderPath(instance, folderType);

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            _logger.Information("Created {FolderType} folder for instance '{InstanceName}' at {FolderPath}",
                folderType, instance.Name, folderPath);
        }
    }

    /// <summary>
    ///     Lists all resources in a folder.
    /// </summary>
    public async Task<List<ResourceItem>> ListResourcesAsync(Instance instance, ResourceFolderType folderType)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        var folderPath = GetResourceFolderPath(instance, folderType);
        var resources = new List<ResourceItem>();

        if (!Directory.Exists(folderPath))
        {
            _logger.Debug("Resource folder does not exist: {FolderPath}", folderPath);
            return resources;
        }

        // Load metadata if it exists
        var metadata = await LoadMetadataAsync(folderPath);

        // Get file patterns based on folder type
        var patterns = GetFilePatterns(folderType);

        foreach (var pattern in patterns)
        {
            foreach (var file in Directory.GetFiles(folderPath, pattern))
            {
                var fileName = Path.GetFileName(file);
                var fileInfo = new FileInfo(file);

                // Check if we have cached metadata
                var resourceItem = metadata.FirstOrDefault(m => m.Name == fileName);
                if (resourceItem == null)
                {
                    resourceItem = new ResourceItem(file, fileName)
                    {
                        SizeBytes = fileInfo.Length,
                        DateAdded = fileInfo.CreationTimeUtc,
                        LastModified = fileInfo.LastWriteTimeUtc
                    };
                }
                else
                {
                    resourceItem.Path = file;
                    resourceItem.SizeBytes = fileInfo.Length;
                    resourceItem.LastModified = fileInfo.LastWriteTimeUtc;
                }

                resources.Add(resourceItem);
            }
        }

        // For worlds, also scan directories
        if (folderType == ResourceFolderType.Worlds)
        {
            foreach (var dir in Directory.GetDirectories(folderPath))
            {
                var dirInfo = new DirectoryInfo(dir);
                var worldName = dirInfo.Name;

                // Skip non-world directories
                if (worldName.StartsWith(".") || worldName == "datapacks")
                    continue;

                // Check for level.dat to confirm it's a world
                var levelDatPath = Path.Combine(dir, "level.dat");
                if (!File.Exists(levelDatPath))
                    continue;

                var resourceItem = metadata.FirstOrDefault(m => m.Name == worldName);
                if (resourceItem == null)
                {
                    resourceItem = new ResourceItem(dir, worldName)
                    {
                        SizeBytes = GetDirectorySize(dir),
                        DateAdded = dirInfo.CreationTimeUtc,
                        LastModified = dirInfo.LastWriteTimeUtc
                    };
                }
                else
                {
                    resourceItem.Path = dir;
                    resourceItem.SizeBytes = GetDirectorySize(dir);
                    resourceItem.LastModified = dirInfo.LastWriteTimeUtc;
                }

                // Enrich with NBT data from level.dat
                try
                {
                    var levelDatPath = Path.Combine(dir, "level.dat");
                    if (File.Exists(levelDatPath))
                    {
                        var nbt = NbtReader.ReadCompressed(levelDatPath);
                        var (levelName, gameMode, lastPlayed) = NbtReader.ExtractLevelInfo(nbt);
                        if (levelName != null) resourceItem.LevelName = levelName;
                        if (gameMode != null) resourceItem.GameMode = gameMode;
                        if (lastPlayed.HasValue) resourceItem.LastPlayed = lastPlayed;
                    }
                }
                catch { /* NBT parse failure is non-fatal */ }

                resources.Add(resourceItem);
            }
        }

        _logger.Debug("Found {Count} resources in {FolderType} folder for instance '{InstanceName}'",
            resources.Count, folderType, instance.Name);

        return resources;
    }

    /// <summary>
    ///     Enables or disables a resource (by renaming).
    /// </summary>
    public bool ToggleResource(ResourceItem resource, bool enable)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        if (!File.Exists(resource.Path) && !Directory.Exists(resource.Path))
        {
            _logger.Warning("Resource not found: {Path}", resource.Path);
            return false;
        }

        try
        {
            var isEnabled = !resource.Path.EndsWith(".disabled");

            if (enable && !isEnabled)
            {
                // Remove .disabled suffix
                var newPath = resource.Path.Substring(0, resource.Path.Length - 9);

                if (File.Exists(resource.Path))
                    File.Move(resource.Path, newPath);
                else
                    Directory.Move(resource.Path, newPath);

                resource.Path = newPath;
                resource.IsEnabled = true;
                _logger.Information("Enabled resource: {Name}", resource.Name);
            }
            else if (!enable && isEnabled)
            {
                // Add .disabled suffix
                var newPath = resource.Path + ".disabled";

                if (File.Exists(resource.Path))
                    File.Move(resource.Path, newPath);
                else
                    Directory.Move(resource.Path, newPath);

                resource.Path = newPath;
                resource.IsEnabled = false;
                _logger.Information("Disabled resource: {Name}", resource.Name);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to toggle resource: {Name}", resource.Name);
            return false;
        }
    }

    /// <summary>
    ///     Deletes a resource.
    /// </summary>
    public bool DeleteResource(ResourceItem resource, bool createBackup = true)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        if (!File.Exists(resource.Path) && !Directory.Exists(resource.Path))
        {
            _logger.Warning("Resource not found: {Path}", resource.Path);
            return false;
        }

        try
        {
            // Create backup if requested
            if (createBackup)
            {
                var backupDir = Path.Combine(Path.GetDirectoryName(resource.Path)!, ".backups");
                Directory.CreateDirectory(backupDir);

                var backupName = $"{Path.GetFileName(resource.Path)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
                var backupPath = Path.Combine(backupDir, backupName);

                if (File.Exists(resource.Path))
                    File.Copy(resource.Path, backupPath);
                else
                    CopyDirectory(resource.Path, backupPath);

                _logger.Information("Created backup of resource '{Name}' at {BackupPath}", resource.Name, backupPath);
            }

            // Delete the resource
            if (File.Exists(resource.Path))
                File.Delete(resource.Path);
            else
                Directory.Delete(resource.Path, true);

            _logger.Information("Deleted resource: {Name}", resource.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete resource: {Name}", resource.Name);
            return false;
        }
    }

    /// <summary>
    ///     Imports a resource into an instance folder.
    /// </summary>
    public async Task<ResourceItem?> ImportResourceAsync(
        Instance instance,
        ResourceFolderType folderType,
        string sourcePath,
        bool copyFile = true)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be empty.", nameof(sourcePath));

        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            _logger.Error("Source not found: {SourcePath}", sourcePath);
            return null;
        }

        var folderPath = GetResourceFolderPath(instance, folderType);
        EnsureResourceFolder(instance, folderType);

        try
        {
            var fileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(folderPath, fileName);

            // Check if already exists
            if (File.Exists(destPath) || Directory.Exists(destPath))
            {
                _logger.Warning("Resource already exists: {FileName}", fileName);
                return null;
            }

            // Copy or move the resource
            if (File.Exists(sourcePath))
            {
                if (copyFile)
                    File.Copy(sourcePath, destPath);
                else
                    File.Move(sourcePath, destPath);
            }
            else
            {
                if (copyFile)
                    CopyDirectory(sourcePath, destPath);
                else
                    Directory.Move(sourcePath, destPath);
            }

            var fileInfo = new FileInfo(destPath);
            var resource = new ResourceItem(destPath, fileName)
            {
                SizeBytes = File.Exists(destPath) ? fileInfo.Length : GetDirectorySize(destPath),
                DateAdded = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            _logger.Information("Imported resource '{FileName}' to {FolderType} folder", fileName, folderType);
            return resource;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to import resource from {SourcePath}", sourcePath);
            return null;
        }
    }

    /// <summary>
    ///     Saves metadata for resources in a folder.
    /// </summary>
    public async Task SaveMetadataAsync(string folderPath, List<ResourceItem> resources)
    {
        try
        {
            var metadataPath = Path.Combine(folderPath, MetadataFileName);
            var json = JsonSerializer.Serialize(resources, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metadataPath, json);
            _logger.Debug("Saved metadata for {Count} resources in {FolderPath}", resources.Count, folderPath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save metadata for {FolderPath}", folderPath);
        }
    }

    /// <summary>
    ///     Loads metadata for resources in a folder.
    /// </summary>
    private async Task<List<ResourceItem>> LoadMetadataAsync(string folderPath)
    {
        try
        {
            var metadataPath = Path.Combine(folderPath, MetadataFileName);

            if (!File.Exists(metadataPath))
                return new List<ResourceItem>();

            var json = await File.ReadAllTextAsync(metadataPath);
            var metadata = JsonSerializer.Deserialize<List<ResourceItem>>(json);
            return metadata ?? new List<ResourceItem>();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load metadata for {FolderPath}", folderPath);
            return new List<ResourceItem>();
        }
    }

    /// <summary>
    ///     Gets file patterns for a resource type.
    /// </summary>
    private string[] GetFilePatterns(ResourceFolderType folderType)
    {
        return folderType switch
        {
            ResourceFolderType.Mods => new[] { "*.jar", "*.litemod" },
            ResourceFolderType.ResourcePacks => new[] { "*.zip" },
            ResourceFolderType.ShaderPacks => new[] { "*.zip" },
            ResourceFolderType.TexturePacks => new[] { "*.zip" },
            ResourceFolderType.DataPacks => new[] { "*.zip" },
            ResourceFolderType.Screenshots => new[] { "*.png", "*.jpg", "*.jpeg" },
            ResourceFolderType.Worlds => Array.Empty<string>(), // Handled separately
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    ///     Recursively calculates directory size.
    /// </summary>
    private long GetDirectorySize(string path)
    {
        var dirInfo = new DirectoryInfo(path);
        long size = 0;

        // Add file sizes
        foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
        {
            size += file.Length;
        }

        return size;
    }

    /// <summary>
    ///     Recursively copies a directory.
    /// </summary>
    private void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, fileName);
            File.Copy(file, destFile, true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            var destSubDir = Path.Combine(destDir, dirName);
            CopyDirectory(subDir, destSubDir);
        }
    }

    public async Task<bool> CopyWorldAsync(Instance source, Instance destination, string worldName)
    {
        var srcPath = Path.Combine(GetResourceFolderPath(source, ResourceFolderType.Worlds), worldName);
        var destPath = Path.Combine(GetResourceFolderPath(destination, ResourceFolderType.Worlds), worldName);

        if (!Directory.Exists(srcPath))
        {
            _logger.Warning("Source world not found: {SrcPath}", srcPath);
            return false;
        }

        if (Directory.Exists(destPath))
        {
            _logger.Warning("World already exists in destination: {DestPath}", destPath);
            return false;
        }

        try
        {
            CopyDirectory(srcPath, destPath);
            _logger.Information("Copied world '{WorldName}' from '{Source}' to '{Destination}'",
                worldName, source.Name, destination.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to copy world '{WorldName}'", worldName);
            return false;
        }
    }

    public async Task<bool> ExportWorldAsync(Instance instance, string worldName, string destZipPath)
    {
        var worldPath = Path.Combine(GetResourceFolderPath(instance, ResourceFolderType.Worlds), worldName);

        if (!Directory.Exists(worldPath))
        {
            _logger.Warning("World not found: {WorldPath}", worldPath);
            return false;
        }

        try
        {
            if (File.Exists(destZipPath)) File.Delete(destZipPath);
            ZipFile.CreateFromDirectory(worldPath, destZipPath);
            _logger.Information("Exported world '{WorldName}' to {DestZipPath}", worldName, destZipPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to export world '{WorldName}'", worldName);
            return false;
        }
    }
}
