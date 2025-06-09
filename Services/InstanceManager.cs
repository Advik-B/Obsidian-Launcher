using System;
using System.Collections.Generic;
using System.IO;
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
    private readonly ILogger _logger;

    public InstanceManager(LauncherConfig launcherConfig, AssetManager assetManager, LibraryManager libraryManager)
    {
        _launcherConfig = launcherConfig ?? throw new ArgumentNullException(nameof(launcherConfig));
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
        _logger = LogHelper.GetLogger<InstanceManager>();
        Directory.CreateDirectory(_launcherConfig.InstancesRootDir);
        _logger.Verbose("InstanceManager initialized. Instances root: {InstancesRootDir}",
            _launcherConfig.InstancesRootDir);
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
        MinecraftVersion mcVersion,
        IProgress<AssetDownloadProgress> assetProgress = null,
        IProgress<LibraryProcessingProgress> libraryProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        if (mcVersion == null) throw new ArgumentNullException(nameof(mcVersion));

        _logger.Information("--- Syncing instance '{InstanceName}' for Minecraft {VersionId} ---", instance.Name,
            mcVersion.Id);

        _logger.Information("[Sync] Ensuring assets for Minecraft {VersionId}...", mcVersion.Id);
        var assetsOk = await _assetManager.EnsureAssetsAsync(mcVersion, assetProgress, cancellationToken);
        if (!assetsOk || cancellationToken.IsCancellationRequested)
        {
            _logger.Error("[Sync] Asset processing failed or was cancelled for instance '{InstanceName}'.",
                instance.Name);
            return (false, null, null);
        }

        _logger.Information("[Sync] Assets ensured for instance '{InstanceName}'.", instance.Name);

        _logger.Information("[Sync] Ensuring client JAR for Minecraft {VersionId}...", mcVersion.Id);
        var clientJarPath = await _assetManager.EnsureClientJarAsync(mcVersion, cancellationToken);
        if (string.IsNullOrEmpty(clientJarPath) || cancellationToken.IsCancellationRequested)
        {
            _logger.Error("[Sync] Client JAR processing failed or was cancelled for instance '{InstanceName}'.",
                instance.Name);
            return (false, null, null);
        }

        _logger.Information("[Sync] Client JAR ensured for instance '{InstanceName}': {ClientJarPath}", instance.Name,
            clientJarPath);

        _logger.Information(
            "[Sync] Ensuring libraries and extracting natives for instance '{InstanceName}' (Natives Path: {NativesPath})...",
            instance.Name, instance.NativesPath);
        Directory.CreateDirectory(instance.NativesPath);
        var libraryJarPaths =
            await _libraryManager.EnsureLibrariesAsync(mcVersion, instance.NativesPath, libraryProgress,
                cancellationToken);
        if (libraryJarPaths == null || cancellationToken.IsCancellationRequested)
        {
            _logger.Error(
                "[Sync] Library processing or native extraction failed or was cancelled for instance '{InstanceName}'.",
                instance.Name);
            return (false, clientJarPath, null);
        }

        _logger.Information("[Sync] Libraries ensured and natives extracted for instance '{InstanceName}'.",
            instance.Name);

        _logger.Information("--- Instance '{InstanceName}' synced successfully for Minecraft {VersionId} ---",
            instance.Name, mcVersion.Id);
        return (true, clientJarPath, libraryJarPaths);
    }


    public async Task<Instance> CreateInstanceAsync( // playerName parameter removed
        string name,
        string minecraftVersionId,
        MinecraftVersion mcVersion,
        IProgress<AssetDownloadProgress> assetProgress = null,
        IProgress<LibraryProcessingProgress> libraryProgress = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedName = SanitizeName(name);
        _logger.Information(
            "Attempting to create instance: Name='{InstanceName}' (Sanitized: '{SanitizedName}'), Version='{MinecraftVersionId}'",
            name, sanitizedName, minecraftVersionId);

        var instancePath = GetInstancePath(sanitizedName);

        if (Directory.Exists(instancePath))
        {
            _logger.Error(
                "Instance directory '{InstancePath}' already exists. Creation implies it shouldn't. Use GetOrCreateInstanceAsync or ensure name is unique.",
                instancePath);
            return null;
        }

        Directory.CreateDirectory(instancePath);
        Directory.CreateDirectory(Path.Combine(instancePath, "natives"));
        Directory.CreateDirectory(Path.Combine(instancePath, "logs"));

        // Create global resourcepacks and shaderpacks directories if they don't exist
        var globalResourcepacks = Path.Combine(_launcherConfig.DataRootDir, "resourcepacks");
        var globalShaderpacks = Path.Combine(_launcherConfig.DataRootDir, "shaderpacks");
        Directory.CreateDirectory(globalResourcepacks);
        Directory.CreateDirectory(globalShaderpacks);
        // Link instance resourcepacks and shaderpacks to global
        var instanceResourcepacks = Path.Combine(instancePath, "resourcepacks");
        var instanceShaderpacks = Path.Combine(instancePath, "shaderpacks");
        FolderLinker.CreateFolderLink(instanceResourcepacks, globalResourcepacks);
        FolderLinker.CreateFolderLink(instanceShaderpacks, globalShaderpacks);

        var newInstance = new Instance
        {
            Name = name,
            MinecraftVersionId = minecraftVersionId,
            InstancePath = Path.GetFullPath(instancePath),
            // PlayerName removed
            CreationDate = DateTime.UtcNow,
            LastPlayedDate = DateTime.MinValue,
            TotalPlaytime = TimeSpan.Zero,
            LastSessionPlaytime = TimeSpan.Zero
        };

        var saved = await SaveInstanceAsync(newInstance);
        if (!saved)
        {
            _logger.Error("Failed to save initial metadata for new instance '{InstanceName}'. Cleaning up directory.",
                newInstance.Name);
            try
            {
                Directory.Delete(instancePath, true);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to cleanup instance directory {InstancePath} after save failure.",
                    instancePath);
            }

            return null;
        }

        _logger.Information("Initial metadata for instance '{InstanceName}' saved. Proceeding to sync.",
            newInstance.Name);

        var (syncSuccess, _, _) =
            await SyncInstanceAsync(newInstance, mcVersion, assetProgress, libraryProgress, cancellationToken);
        if (!syncSuccess)
        {
            _logger.Error(
                "Sync failed during creation of instance '{InstanceName}'. The instance directory and metadata exist but may be incomplete.",
                newInstance.Name);
            return newInstance;
        }

        _logger.Information("Successfully created and synced new instance: '{InstanceName}' at {InstancePath}",
            newInstance.Name, newInstance.InstancePath);
        return newInstance;
    }

    public async Task<(Instance Instance, string ClientJarPath, List<string> LibraryJarPaths)>
        GetOrCreateInstanceAsync( // playerName parameter removed
            string name,
            string minecraftVersionId,
            MinecraftVersion mcVersion,
            IProgress<AssetDownloadProgress> assetProgress = null,
            IProgress<LibraryProcessingProgress> libraryProgress = null,
            CancellationToken cancellationToken = default)
    {
        var sanitizedName = SanitizeName(name);
        var instance = await LoadInstanceAsync(sanitizedName);
        var needsCreation = false;

        if (instance != null)
        {
            if (instance.MinecraftVersionId != minecraftVersionId)
            {
                _logger.Error(
                    "Instance '{InstanceName}' exists but for Minecraft version '{ExistingMcVersion}'. Expected '{McVersion}'. Cannot proceed with this name for a different version.",
                    instance.Name, instance.MinecraftVersionId, minecraftVersionId);
                return (null, null, null);
            }

            // PlayerName update logic removed
            _logger.Information("Loaded existing instance: '{InstanceName}'. Will ensure it's synced.", instance.Name);
        }
        else
        {
            _logger.Information("Instance '{SanitizedName}' not found. Creating new instance.", sanitizedName);
            // Call CreateInstanceAsync without playerName
            instance = await CreateInstanceAsync(name, minecraftVersionId, mcVersion, assetProgress, libraryProgress,
                cancellationToken);
            if (instance == null)
            {
                _logger.Error("Failed to create instance '{SanitizedName}'.", sanitizedName);
                return (null, null, null);
            }

            needsCreation = true;
        }

        if (instance == null) return (null, null, null);

        if (!needsCreation)
        {
            _logger.Information("Ensuring existing instance '{InstanceName}' is synced...", instance.Name);
            var (syncSuccess, clientJarPath, libraryJarPaths) = await SyncInstanceAsync(instance, mcVersion,
                assetProgress, libraryProgress, cancellationToken);
            if (!syncSuccess)
            {
                _logger.Error("Sync failed for existing instance '{InstanceName}'. Launch might fail.", instance.Name);
                return (instance, clientJarPath, libraryJarPaths);
            }

            return (instance, clientJarPath, libraryJarPaths);
        }
        else
        {
            var (syncSuccess, clientJarPath, libraryJarPaths) = await SyncInstanceAsync(instance, mcVersion,
                assetProgress, libraryProgress, cancellationToken);
            if (!syncSuccess)
                _logger.Error(
                    "Sync after creation seems to have failed for instance '{InstanceName}'. This is unexpected if creation reported success.",
                    instance.Name);
            return (instance, clientJarPath, libraryJarPaths);
        }
    }


    public async Task<Instance> LoadInstanceAsync(string name)
    {
        var instancePath = GetInstancePath(name);
        var metadataFilePath = Path.Combine(instancePath, InstanceMetadataFileName);

        if (!File.Exists(metadataFilePath))
        {
            _logger.Verbose(
                "Instance metadata file not found for '{InstanceName}' at {FilePath}. Assuming instance does not exist or is incomplete.",
                name, metadataFilePath);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(metadataFilePath);
            var instance = JsonSerializer.Deserialize<Instance>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (instance != null)
            {
                instance.InstancePath = Path.GetFullPath(instancePath);
                _logger.Verbose(
                    "Successfully loaded instance '{InstanceName}' (Original Name: {OriginalName}) from {FilePath}",
                    name, instance.Name, metadataFilePath);
            }

            return instance;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load or deserialize instance metadata for '{InstanceName}' from {FilePath}",
                name, metadataFilePath);
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
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(instance, options);
            await File.WriteAllTextAsync(metadataFilePath, json);
            _logger.Verbose("Successfully saved instance metadata for '{InstanceName}' to {FilePath}", instance.Name,
                metadataFilePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save instance metadata for '{InstanceName}' to {FilePath}", instance.Name,
                metadataFilePath);
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

        _logger.Information("Loaded {Count} instances from {InstancesRootDir}", instances.Count,
            _launcherConfig.InstancesRootDir);
        return instances;
    }

    public async Task UpdateLastPlayedAsync(Instance instance, TimeSpan sessionDuration)
    {
        if (instance == null) return;
        instance.LastPlayedDate = DateTime.UtcNow;
        instance.LastSessionPlaytime = sessionDuration;
        instance.TotalPlaytime += sessionDuration;
        await SaveInstanceAsync(instance);
        _logger.Information(
            "Updated playtime for instance '{InstanceName}'. Last Session: {LastSessionPlaytimeFormat}, Total Playtime: {TotalPlaytimeFormat}",
            instance.Name, instance.LastSessionPlaytime.ToString(@"hh\:mm\:ss"),
            instance.TotalPlaytime.ToString(@"d\.hh\:mm\:ss"));
    }
}