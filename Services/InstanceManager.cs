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

public class InstanceManager
{
    private const string InstanceMetadataFileName = "instance.json";
    private readonly AssetManager _assetManager;
    private readonly LauncherConfig _launcherConfig;
    private readonly LibraryManager _libraryManager;
    private readonly ModLoaderService _modLoaderService; // New service
    private readonly HttpManager _httpManager; // Needed for fetching base game version
    private readonly ILogger _logger;

    public InstanceManager(LauncherConfig launcherConfig, AssetManager assetManager, LibraryManager libraryManager, HttpManager httpManager)
    {
        _launcherConfig = launcherConfig ?? throw new ArgumentNullException(nameof(launcherConfig));
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _logger = LogHelper.GetLogger<InstanceManager>();
        _modLoaderService = new ModLoaderService(_httpManager); // Initialize new service
        Directory.CreateDirectory(_launcherConfig.InstancesRootDir);
        _logger.Verbose("InstanceManager initialized. Instances root: {InstancesRootDir}", _launcherConfig.InstancesRootDir);
    }
    
    private string GetInstancePath(string instanceName)
    {
        var sanitizedName = SanitizeName(instanceName);
        return Path.Combine(_launcherConfig.InstancesRootDir, sanitizedName);
    }

    public static string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"Unnamed_Instance_{Guid.NewGuid().ToString().Substring(0, 8)}";
            Log.Warning("Instance name was empty or whitespace, sanitized to '{SanitizedName}'", name);
        }

        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Replace(" ", "_").Trim();
    }
    
    public async Task<(bool Success, string? ClientJarPath, List<string>? LibraryJarPaths)> SyncInstanceAsync(
        Instance instance,
        IProgress<AssetDownloadProgress>? assetProgress = null,
        IProgress<LibraryProcessingProgress>? libraryProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));

        _logger.Information("--- Syncing instance '{InstanceName}' ---", instance.Name);

        // 1. Build the launch profile by merging components
        var launchProfile = await BuildLaunchProfileAsync(instance.Components, cancellationToken);
        if (launchProfile == null)
        {
            _logger.Error("Failed to build launch profile for instance '{InstanceName}'.", instance.Name);
            return (false, null, null);
        }

        _logger.Information("[Sync] Ensuring assets for Minecraft {VersionId}...", launchProfile.Id);
        var assetsOk = await _assetManager.EnsureAssetsAsync(launchProfile, assetProgress, cancellationToken);
        if (!assetsOk || cancellationToken.IsCancellationRequested)
        {
            _logger.Error("[Sync] Asset processing failed or was cancelled for instance '{InstanceName}'.", instance.Name);
            return (false, null, null);
        }
        _logger.Information("[Sync] Assets ensured for instance '{InstanceName}'.", instance.Name);

        _logger.Information("[Sync] Ensuring client JAR for Minecraft {VersionId}...", launchProfile.Id);
        var clientJarPath = await _assetManager.EnsureClientJarAsync(launchProfile, cancellationToken);
        if (string.IsNullOrEmpty(clientJarPath) || cancellationToken.IsCancellationRequested)
        {
            _logger.Error("[Sync] Client JAR processing failed or was cancelled for instance '{InstanceName}'.", instance.Name);
            return (false, null, null);
        }
        _logger.Information("[Sync] Client JAR ensured for instance '{InstanceName}': {ClientJarPath}", instance.Name, clientJarPath);

        _logger.Information("[Sync] Ensuring libraries and extracting natives for instance '{InstanceName}' (Natives Path: {NativesPath})...", instance.Name, instance.NativesPath);
        Directory.CreateDirectory(instance.NativesPath);
        var libraryJarPaths = await _libraryManager.EnsureLibrariesAsync(launchProfile, instance.NativesPath, libraryProgress, cancellationToken);
        if (libraryJarPaths == null || cancellationToken.IsCancellationRequested)
        {
            _logger.Error("[Sync] Library processing or native extraction failed or was cancelled for instance '{InstanceName}'.", instance.Name);
            return (false, clientJarPath, null);
        }
        _logger.Information("[Sync] Libraries ensured and natives extracted for instance '{InstanceName}'.", instance.Name);

        _logger.Information("--- Instance '{InstanceName}' synced successfully for Minecraft {VersionId} ---", instance.Name, launchProfile.Id);
        return (true, clientJarPath, libraryJarPaths);
    }
    
    public async Task<LaunchProfile?> BuildLaunchProfileAsync(List<Component> components, CancellationToken cancellationToken)
    {
        var launchProfile = new LaunchProfile();
        _logger.Information("Building launch profile from {ComponentCount} components.", components.Count);

        foreach (var component in components.Where(c => c.IsEnabled))
        {
            MinecraftVersion? componentVersion = null;
            if (component.Uid == "net.minecraft")
            {
                // Fetch base Minecraft version from Mojang
                componentVersion = await GetMinecraftVersionDetailsAsync(component.Version, cancellationToken);
            }
            else
            {
                // Fetch mod loader version
                componentVersion = await _modLoaderService.GetModLoaderVersionAsync(component, cancellationToken);
            }

            if (componentVersion == null)
            {
                _logger.Error("Failed to fetch details for component {Uid} {Version}. Aborting profile build.", component.Uid, component.Version);
                return null;
            }

            launchProfile.MergeFrom(componentVersion);
        }

        if (string.IsNullOrEmpty(launchProfile.Id))
        {
            _logger.Error("Launch profile build failed: No base Minecraft version was loaded.");
            return null;
        }

        return launchProfile;
    }
    
    private async Task<MinecraftVersion?> GetMinecraftVersionDetailsAsync(string versionId, CancellationToken cancellationToken)
    {
        var manifestResponseMsg = await _httpManager.GetAsync("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json", cancellationToken: cancellationToken);
        if (!manifestResponseMsg.IsSuccessStatusCode) return null;
        
        var manifestJsonString = await manifestResponseMsg.Content.ReadAsStringAsync(cancellationToken);
        var versionManifestAll = JsonSerializer.Deserialize<VersionManifest>(manifestJsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        var selectedVersionMeta = versionManifestAll?.Versions.FirstOrDefault(v => v.Id == versionId);
        if (selectedVersionMeta == null) return null;

        var versionDetailsResponseMsg = await _httpManager.GetAsync(selectedVersionMeta.Url, cancellationToken: cancellationToken);
        if (!versionDetailsResponseMsg.IsSuccessStatusCode) return null;

        var versionDetailsJsonString = await versionDetailsResponseMsg.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<MinecraftVersion>(versionDetailsJsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<Instance?> CreateInstanceAsync(
        string name,
        List<Component> components,
        IProgress<AssetDownloadProgress>? assetProgress = null,
        IProgress<LibraryProcessingProgress>? libraryProgress = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedName = SanitizeName(name);
        _logger.Information("Attempting to create instance: Name='{InstanceName}' (Sanitized: '{SanitizedName}')", name, sanitizedName);

        var instancePath = GetInstancePath(sanitizedName);

        if (Directory.Exists(instancePath))
        {
            _logger.Error("Instance directory '{InstancePath}' already exists. Creation implies it shouldn't. Use GetOrCreateInstanceAsync or ensure name is unique.", instancePath);
            return null;
        }

        Directory.CreateDirectory(instancePath);
        Directory.CreateDirectory(Path.Combine(instancePath, "natives"));
        Directory.CreateDirectory(Path.Combine(instancePath, "logs"));
        
        var globalResourcepacks = Path.Combine(_launcherConfig.DataRootDir, "resourcepacks");
        var globalShaderpacks = Path.Combine(_launcherConfig.DataRootDir, "shaderpacks");
        Directory.CreateDirectory(globalResourcepacks);
        Directory.CreateDirectory(globalShaderpacks);
        var instanceResourcepacks = Path.Combine(instancePath, "resourcepacks");
        var instanceShaderpacks = Path.Combine(instancePath, "shaderpacks");
        FolderLinker.CreateFolderLink(instanceResourcepacks, globalResourcepacks);
        FolderLinker.CreateFolderLink(instanceShaderpacks, globalShaderpacks);

        var newInstance = new Instance
        {
            Name = name,
            InstancePath = Path.GetFullPath(instancePath),
            Components = components,
            CreationDate = DateTime.UtcNow
        };
        
        var saved = await SaveInstanceAsync(newInstance);
        if (!saved)
        {
            _logger.Error("Failed to save initial metadata for new instance '{InstanceName}'. Cleaning up directory.", newInstance.Name);
            try { Directory.Delete(instancePath, true); }
            catch (Exception ex) { _logger.Error(ex, "Failed to cleanup instance directory {InstancePath} after save failure.", instancePath); }
            return null;
        }

        _logger.Information("Initial metadata for instance '{InstanceName}' saved. Proceeding to sync.", newInstance.Name);
        
        var (syncSuccess, _, _) = await SyncInstanceAsync(newInstance, assetProgress, libraryProgress, cancellationToken);
        if (!syncSuccess)
        {
            _logger.Error("Sync failed during creation of instance '{InstanceName}'. The instance directory and metadata exist but may be incomplete.", newInstance.Name);
        }

        _logger.Information("Successfully created and synced new instance: '{InstanceName}' at {InstancePath}", newInstance.Name, newInstance.InstancePath);
        return newInstance;
    }
    
    public async Task<(Instance? Instance, string? ClientJarPath, List<string>? LibraryJarPaths)> GetOrCreateInstanceAsync(
        string name,
        List<Component> components,
        IProgress<AssetDownloadProgress>? assetProgress = null,
        IProgress<LibraryProcessingProgress>? libraryProgress = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedName = SanitizeName(name);
        var instance = await LoadInstanceAsync(sanitizedName);
        var needsCreation = instance == null;

        if (instance != null)
        {
            // TODO: A more robust check might compare all components
            var existingMcComp = instance.Components.FirstOrDefault(c => c.Uid == "net.minecraft");
            var newMcComp = components.FirstOrDefault(c => c.Uid == "net.minecraft");
            if (existingMcComp?.Version != newMcComp?.Version)
            {
                _logger.Error(
                    "Instance '{InstanceName}' exists but for Minecraft version '{ExistingMcVersion}'. Expected '{NewMcVersion}'. Cannot proceed with this name for a different version.",
                    instance.Name, existingMcComp?.Version, newMcComp?.Version);
                return (null, null, null);
            }
            _logger.Information("Loaded existing instance: '{InstanceName}'. Will ensure it's synced.", instance.Name);
        }
        else
        {
            _logger.Information("Instance '{SanitizedName}' not found. Creating new instance.", sanitizedName);
            instance = await CreateInstanceAsync(name, components, assetProgress, libraryProgress, cancellationToken);
        }
        
        if (instance == null) return (null, null, null);
        
        // Always sync, even if just created (CreateInstanceAsync already does this, but this ensures consistency)
        _logger.Information("Ensuring instance '{InstanceName}' is synced...", instance.Name);
        var (syncSuccess, clientJarPath, libraryJarPaths) = await SyncInstanceAsync(instance, assetProgress, libraryProgress, cancellationToken);
        
        if (!syncSuccess)
        {
            _logger.Error("Sync failed for instance '{InstanceName}'. Launch might fail.", instance.Name);
        }
        
        return (instance, clientJarPath, libraryJarPaths);
    }
    
    // Unchanged methods...
    public async Task<Instance?> LoadInstanceAsync(string name)
    {
        var instancePath = GetInstancePath(name);
        var metadataFilePath = Path.Combine(instancePath, InstanceMetadataFileName);

        if (!File.Exists(metadataFilePath))
        {
            _logger.Verbose("Instance metadata file not found for '{InstanceName}' at {FilePath}. Assuming instance does not exist or is incomplete.", name, metadataFilePath);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(metadataFilePath);
            var instance = JsonSerializer.Deserialize<Instance>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (instance != null)
            {
                instance.InstancePath = Path.GetFullPath(instancePath);
                _logger.Verbose("Successfully loaded instance '{InstanceName}' (Original Name: {OriginalName}) from {FilePath}", name, instance.Name, metadataFilePath);
            }
            return instance;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load or deserialize instance metadata for '{InstanceName}' from {FilePath}", name, metadataFilePath);
            return null;
        }
    }

    public async Task<bool> SaveInstanceAsync(Instance instance)
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        if (string.IsNullOrWhiteSpace(instance.InstancePath))
        {
            _logger.Error("Instance path is not set for instance '{InstanceName}'. Cannot save.", instance.Name);
            return false;
        }

        var metadataFilePath = Path.Combine(instance.InstancePath, InstanceMetadataFileName);
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(instance, options);
            await File.WriteAllTextAsync(metadataFilePath, json);
            _logger.Verbose("Successfully saved instance metadata for '{InstanceName}' to {FilePath}", instance.Name, metadataFilePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save instance metadata for '{InstanceName}' to {FilePath}", instance.Name, metadataFilePath);
            return false;
        }
    }

    public async Task<List<Instance>> GetAllInstancesAsync()
    {
        var instances = new List<Instance>();
        if (!Directory.Exists(_launcherConfig.InstancesRootDir))
        {
            _logger.Information("Instances root directory does not exist. No instances to load.");
            return instances;
        }

        foreach (var dir in Directory.EnumerateDirectories(_launcherConfig.InstancesRootDir))
        {
            var instanceDirName = Path.GetFileName(dir);
            var instance = await LoadInstanceAsync(instanceDirName);
            if (instance != null) instances.Add(instance);
        }

        _logger.Information("Loaded {Count} instances from {InstancesRootDir}", instances.Count, _launcherConfig.InstancesRootDir);
        return instances;
    }

    public async Task UpdateLastPlayedAsync(Instance instance, TimeSpan sessionDuration)
    {
        if (instance == null) return;
        instance.LastPlayedDate = DateTime.UtcNow;
        instance.LastSessionPlaytime = sessionDuration;
        instance.TotalPlaytime += sessionDuration;
        await SaveInstanceAsync(instance);
        _logger.Information("Updated playtime for instance '{InstanceName}'. Last Session: {LastSessionPlaytimeFormat}, Total Playtime: {TotalPlaytimeFormat}",
            instance.Name, instance.LastSessionPlaytime.ToString(@"hh\:mm\:ss"), instance.TotalPlaytime.ToString(@"d\.hh\:mm\:ss"));
    }

    /// <summary>
    ///     Copies an instance to create a new instance with the same configuration.
    /// </summary>
    /// <param name="sourceInstanceName">Name of the source instance to copy.</param>
    /// <param name="newInstanceName">Name for the new copied instance.</param>
    /// <param name="copyPlaytimeData">Whether to copy playtime data (default: false).</param>
    /// <returns>The newly created instance, or null if copy failed.</returns>
    public async Task<Instance?> CopyInstanceAsync(string sourceInstanceName, string newInstanceName, bool copyPlaytimeData = false)
    {
        if (string.IsNullOrWhiteSpace(sourceInstanceName))
            throw new ArgumentException("Source instance name cannot be empty.", nameof(sourceInstanceName));
        if (string.IsNullOrWhiteSpace(newInstanceName))
            throw new ArgumentException("New instance name cannot be empty.", nameof(newInstanceName));

        _logger.Information("Copying instance '{SourceName}' to '{NewName}'...", sourceInstanceName, newInstanceName);

        // Load source instance
        var sourceInstance = await LoadInstanceAsync(sourceInstanceName);
        if (sourceInstance == null)
        {
            _logger.Error("Source instance '{SourceName}' not found.", sourceInstanceName);
            return null;
        }

        var sourcePath = GetInstancePath(sourceInstanceName);
        var newPath = GetInstancePath(newInstanceName);

        // Check if destination already exists
        if (Directory.Exists(newPath))
        {
            _logger.Error("Instance '{NewName}' already exists at {NewPath}.", newInstanceName, newPath);
            return null;
        }

        try
        {
            // Copy entire directory structure
            CopyDirectory(sourcePath, newPath);

            // Load the copied instance
            var newInstance = await LoadInstanceAsync(newInstanceName);
            if (newInstance == null)
            {
                _logger.Error("Failed to load copied instance '{NewName}'.", newInstanceName);
                return null;
            }

            // Update instance metadata
            newInstance.Id = Guid.NewGuid().ToString();
            newInstance.Name = newInstanceName;
            newInstance.CreationDate = DateTime.UtcNow;
            newInstance.LastModifiedDate = DateTime.UtcNow;

            if (!copyPlaytimeData)
            {
                newInstance.TotalPlaytime = TimeSpan.Zero;
                newInstance.LastSessionPlaytime = TimeSpan.Zero;
                newInstance.LastPlayedDate = DateTime.MinValue;
            }

            // Save updated metadata
            await SaveInstanceAsync(newInstance);

            _logger.Information("Successfully copied instance '{SourceName}' to '{NewName}' at {NewPath}", 
                sourceInstanceName, newInstanceName, newPath);
            return newInstance;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to copy instance '{SourceName}' to '{NewName}'", sourceInstanceName, newInstanceName);
            
            // Clean up if copy failed
            if (Directory.Exists(newPath))
            {
                try
                {
                    Directory.Delete(newPath, true);
                    _logger.Information("Cleaned up failed copy at {NewPath}", newPath);
                }
                catch (Exception cleanupEx)
                {
                    _logger.Warning(cleanupEx, "Failed to clean up failed copy at {NewPath}", newPath);
                }
            }

            return null;
        }
    }

    /// <summary>
    ///     Deletes an instance and all its data.
    /// </summary>
    /// <param name="instanceName">Name of the instance to delete.</param>
    /// <param name="createBackup">Whether to create a backup before deletion (default: true).</param>
    /// <returns>True if deletion was successful, false otherwise.</returns>
    public async Task<bool> DeleteInstanceAsync(string instanceName, bool createBackup = true)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new ArgumentException("Instance name cannot be empty.", nameof(instanceName));

        var instancePath = GetInstancePath(instanceName);

        if (!Directory.Exists(instancePath))
        {
            _logger.Warning("Instance '{InstanceName}' not found at {InstancePath}.", instanceName, instancePath);
            return false;
        }

        _logger.Information("Deleting instance '{InstanceName}' at {InstancePath}...", instanceName, instancePath);

        try
        {
            // Create backup if requested
            if (createBackup)
            {
                var backupPath = Path.Combine(_launcherConfig.BaseDataPath, "backups", "instances", 
                    $"{SanitizeName(instanceName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
                
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                CopyDirectory(instancePath, backupPath);
                _logger.Information("Created backup of instance '{InstanceName}' at {BackupPath}", instanceName, backupPath);
            }

            // Delete the instance directory
            Directory.Delete(instancePath, true);
            _logger.Information("Successfully deleted instance '{InstanceName}'", instanceName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete instance '{InstanceName}'", instanceName);
            return false;
        }
    }

    /// <summary>
    ///     Updates instance metadata (name, notes, tags, etc.).
    /// </summary>
    /// <param name="instanceName">Current name of the instance.</param>
    /// <param name="updateAction">Action to update the instance properties.</param>
    /// <param name="renameInstance">Whether to rename the instance directory if name changes (default: false).</param>
    /// <returns>True if update was successful, false otherwise.</returns>
    public async Task<bool> UpdateInstanceMetadataAsync(string instanceName, Action<Instance> updateAction, bool renameInstance = false)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new ArgumentException("Instance name cannot be empty.", nameof(instanceName));

        var instance = await LoadInstanceAsync(instanceName);
        if (instance == null)
        {
            _logger.Error("Instance '{InstanceName}' not found.", instanceName);
            return false;
        }

        var oldName = instance.Name;
        var oldPath = instance.InstancePath;

        // Apply updates
        updateAction(instance);
        instance.LastModifiedDate = DateTime.UtcNow;

        // Handle renaming if name changed and renaming is requested
        if (renameInstance && !string.Equals(oldName, instance.Name, StringComparison.OrdinalIgnoreCase))
        {
            var newPath = GetInstancePath(instance.Name);

            if (Directory.Exists(newPath))
            {
                _logger.Error("Cannot rename instance to '{NewName}': directory already exists at {NewPath}", 
                    instance.Name, newPath);
                return false;
            }

            try
            {
                Directory.Move(oldPath, newPath);
                instance.InstancePath = newPath;
                _logger.Information("Renamed instance directory from '{OldName}' to '{NewName}'", oldName, instance.Name);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to rename instance directory from '{OldName}' to '{NewName}'", 
                    oldName, instance.Name);
                return false;
            }
        }

        // Save updated metadata
        var saved = await SaveInstanceAsync(instance);
        if (saved)
        {
            _logger.Information("Updated metadata for instance '{InstanceName}'", instance.Name);
        }

        return saved;
    }

    /// <summary>
    ///     Exports an instance to a zip file.
    /// </summary>
    /// <param name="instanceName">Name of the instance to export.</param>
    /// <param name="exportPath">Path where the zip file should be created.</param>
    /// <param name="includeConfig">Whether to include configuration files (default: true).</param>
    /// <param name="includeWorlds">Whether to include world saves (default: true).</param>
    /// <param name="includeResourcePacks">Whether to include resource packs (default: false).</param>
    /// <param name="includeScreenshots">Whether to include screenshots (default: false).</param>
    /// <returns>Path to the created zip file, or null if export failed.</returns>
    public async Task<string?> ExportInstanceAsync(
        string instanceName,
        string exportPath,
        bool includeConfig = true,
        bool includeWorlds = true,
        bool includeResourcePacks = false,
        bool includeScreenshots = false)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new ArgumentException("Instance name cannot be empty.", nameof(instanceName));
        if (string.IsNullOrWhiteSpace(exportPath))
            throw new ArgumentException("Export path cannot be empty.", nameof(exportPath));

        var instance = await LoadInstanceAsync(instanceName);
        if (instance == null)
        {
            _logger.Error("Instance '{InstanceName}' not found for export.", instanceName);
            return null;
        }

        var instancePath = GetInstancePath(instanceName);
        
        try
        {
            // Ensure export directory exists
            var exportDir = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrEmpty(exportDir))
            {
                Directory.CreateDirectory(exportDir);
            }

            // Create temporary directory for export content
            var tempDir = Path.Combine(Path.GetTempPath(), $"instance_export_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Copy instance metadata
                var metadataPath = Path.Combine(instancePath, InstanceMetadataFileName);
                if (File.Exists(metadataPath))
                {
                    File.Copy(metadataPath, Path.Combine(tempDir, InstanceMetadataFileName));
                }

                // Copy configuration files
                if (includeConfig)
                {
                    var configFiles = new[] { "config", "options.txt", "servers.dat", "servers.dat_old" };
                    foreach (var configFile in configFiles)
                    {
                        var sourcePath = Path.Combine(instancePath, configFile);
                        var destPath = Path.Combine(tempDir, configFile);

                        if (File.Exists(sourcePath))
                        {
                            File.Copy(sourcePath, destPath);
                        }
                        else if (Directory.Exists(sourcePath))
                        {
                            CopyDirectory(sourcePath, destPath);
                        }
                    }
                }

                // Copy worlds
                if (includeWorlds)
                {
                    var savesPath = Path.Combine(instancePath, "saves");
                    if (Directory.Exists(savesPath))
                    {
                        CopyDirectory(savesPath, Path.Combine(tempDir, "saves"));
                    }
                }

                // Copy resource packs
                if (includeResourcePacks)
                {
                    var resourcePacksPath = Path.Combine(instancePath, "resourcepacks");
                    if (Directory.Exists(resourcePacksPath))
                    {
                        CopyDirectory(resourcePacksPath, Path.Combine(tempDir, "resourcepacks"));
                    }
                }

                // Copy screenshots
                if (includeScreenshots)
                {
                    var screenshotsPath = Path.Combine(instancePath, "screenshots");
                    if (Directory.Exists(screenshotsPath))
                    {
                        CopyDirectory(screenshotsPath, Path.Combine(tempDir, "screenshots"));
                    }
                }

                // Create zip file
                if (File.Exists(exportPath))
                {
                    File.Delete(exportPath);
                }

                System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, exportPath);
                _logger.Information("Exported instance '{InstanceName}' to {ExportPath}", instanceName, exportPath);
                
                return exportPath;
            }
            finally
            {
                // Clean up temporary directory
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to export instance '{InstanceName}' to {ExportPath}", instanceName, exportPath);
            return null;
        }
    }

    /// <summary>
    ///     Imports an instance from a zip file.
    /// </summary>
    /// <param name="zipPath">Path to the zip file containing the instance.</param>
    /// <param name="instanceName">Name for the imported instance.</param>
    /// <returns>The imported instance, or null if import failed.</returns>
    public async Task<Instance?> ImportInstanceAsync(string zipPath, string instanceName)
    {
        if (string.IsNullOrWhiteSpace(zipPath))
            throw new ArgumentException("Zip path cannot be empty.", nameof(zipPath));
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new ArgumentException("Instance name cannot be empty.", nameof(instanceName));

        if (!File.Exists(zipPath))
        {
            _logger.Error("Zip file not found: {ZipPath}", zipPath);
            return null;
        }

        var instancePath = GetInstancePath(instanceName);

        if (Directory.Exists(instancePath))
        {
            _logger.Error("Instance '{InstanceName}' already exists at {InstancePath}", instanceName, instancePath);
            return null;
        }

        try
        {
            // Extract zip to instance directory
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, instancePath);
            
            // Load instance
            var instance = await LoadInstanceAsync(instanceName);
            if (instance == null)
            {
                _logger.Error("Failed to load imported instance '{InstanceName}'", instanceName);
                return null;
            }

            // Update instance metadata
            instance.Id = Guid.NewGuid().ToString();
            instance.Name = instanceName;
            instance.CreationDate = DateTime.UtcNow;
            instance.LastModifiedDate = DateTime.UtcNow;
            instance.InstancePath = instancePath;

            // Save updated metadata
            await SaveInstanceAsync(instance);

            _logger.Information("Successfully imported instance '{InstanceName}' from {ZipPath}", instanceName, zipPath);
            return instance;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to import instance '{InstanceName}' from {ZipPath}", instanceName, zipPath);
            
            // Clean up if import failed
            if (Directory.Exists(instancePath))
            {
                try
                {
                    Directory.Delete(instancePath, true);
                }
                catch (Exception cleanupEx)
                {
                    _logger.Warning(cleanupEx, "Failed to clean up failed import at {InstancePath}", instancePath);
                }
            }

            return null;
        }
    }

    /// <summary>
    ///     Helper method to recursively copy a directory.
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        // Create destination directory
        Directory.CreateDirectory(destDir);

        // Copy files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, fileName);
            File.Copy(file, destFile, true);
        }

        // Copy subdirectories
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            var destSubDir = Path.Combine(destDir, dirName);
            CopyDirectory(subDir, destSubDir);
        }
    }
}