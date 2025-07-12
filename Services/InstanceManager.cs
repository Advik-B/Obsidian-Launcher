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
    
    public async Task<(bool Success, string ClientJarPath, List<string> LibraryJarPaths)> SyncInstanceAsync(
        Instance instance,
        IProgress<AssetDownloadProgress> assetProgress = null,
        IProgress<LibraryProcessingProgress> libraryProgress = null,
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
    public async Task<Instance> LoadInstanceAsync(string name)
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
}