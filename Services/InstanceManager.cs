using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils; 
using Serilog;

namespace ObsidianLauncher.Services
{
    public class InstanceManager
    {
        private readonly LauncherConfig _launcherConfig;
        private readonly ILogger _logger;
        private const string InstanceMetadataFileName = "instance.json";

        public InstanceManager(LauncherConfig launcherConfig)
        {
            _launcherConfig = launcherConfig ?? throw new ArgumentNullException(nameof(launcherConfig));
            _logger = LogHelper.GetLogger<InstanceManager>();
            Directory.CreateDirectory(_launcherConfig.InstancesRootDir); 
            _logger.Verbose("InstanceManager initialized. Instances root: {InstancesRootDir}", _launcherConfig.InstancesRootDir);
        }

        private string GetInstancePath(string instanceName)
        {
            string sanitizedName = SanitizeName(instanceName);
            return Path.Combine(_launcherConfig.InstancesRootDir, sanitizedName);
        }

        public static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Unnamed_Instance_{Guid.NewGuid().ToString().Substring(0,8)}";
                Log.Warning("Instance name was empty or whitespace, sanitized to '{SanitizedName}'", name);
            }
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Replace(" ", "_").Trim();
        }


        public async Task<Instance> CreateInstanceAsync(string name, string minecraftVersionId, string playerName = null)
        {
            string sanitizedName = SanitizeName(name);
            _logger.Information("Attempting to create instance: Name='{InstanceName}' (Sanitized: '{SanitizedName}'), Version='{MinecraftVersionId}'", name, sanitizedName, minecraftVersionId);
            
            string instancePath = GetInstancePath(sanitizedName); 

            if (Directory.Exists(instancePath))
            {
                var existingInstance = await LoadInstanceAsync(sanitizedName); 
                if (existingInstance != null && existingInstance.MinecraftVersionId == minecraftVersionId)
                {
                    _logger.Information("Instance '{InstanceName}' for version '{MinecraftVersionId}' already exists. Returning existing.", sanitizedName, minecraftVersionId);
                    if (!string.IsNullOrEmpty(playerName) && existingInstance.PlayerName != playerName)
                    {
                        existingInstance.PlayerName = playerName;
                        await SaveInstanceAsync(existingInstance);
                    }
                    return existingInstance;
                }
                _logger.Error("Instance directory '{InstancePath}' already exists but with different metadata or cannot be loaded. Cannot create new instance with this name: {SanitizedName}.", instancePath, sanitizedName);
                return null;
            }

            Directory.CreateDirectory(instancePath);
            Directory.CreateDirectory(Path.Combine(instancePath, "natives"));
            Directory.CreateDirectory(Path.Combine(instancePath, "logs")); 

            var newInstance = new Instance
            {
                Name = name, 
                MinecraftVersionId = minecraftVersionId,
                InstancePath = Path.GetFullPath(instancePath), 
                PlayerName = playerName ?? $"Player_{Random.Shared.Next(100, 999)}", 
                CreationDate = DateTime.UtcNow,
                LastPlayedDate = DateTime.MinValue,
                TotalPlaytime = TimeSpan.Zero,      // Initialize playtime
                LastSessionPlaytime = TimeSpan.Zero // Initialize playtime
            };

            bool saved = await SaveInstanceAsync(newInstance);
            if (saved)
            {
                _logger.Information("Successfully created and saved new instance: '{InstanceName}' at {InstancePath}", newInstance.Name, newInstance.InstancePath);
                return newInstance;
            }
            else
            {
                _logger.Error("Failed to save metadata for new instance '{InstanceName}'. Cleaning up directory.", newInstance.Name);
                try { Directory.Delete(instancePath, true); }
                catch (Exception ex) { _logger.Error(ex, "Failed to cleanup instance directory {InstancePath} after save failure.", instancePath); }
                return null;
            }
        }

        public async Task<Instance> GetOrCreateInstanceAsync(string name, string minecraftVersionId, string playerName = null)
        {
            string sanitizedName = SanitizeName(name);
            var instance = await LoadInstanceAsync(sanitizedName); 
            if (instance != null)
            {
                if (instance.MinecraftVersionId != minecraftVersionId)
                {
                    _logger.Warning("Instance '{InstanceName}' exists but for Minecraft version '{ExistingMcVersion}'. Expected '{McVersion}'. Please use a different instance name or delete the existing one.",
                        instance.Name, instance.MinecraftVersionId, minecraftVersionId);
                    return null; 
                }
                if (!string.IsNullOrEmpty(playerName) && instance.PlayerName != playerName)
                {
                    _logger.Information("Updating player name for instance '{InstanceName}' from '{OldPlayerName}' to '{NewPlayerName}'.", instance.Name, instance.PlayerName, playerName);
                    instance.PlayerName = playerName;
                    await SaveInstanceAsync(instance);
                }
                else if (string.IsNullOrEmpty(instance.PlayerName) && !string.IsNullOrEmpty(playerName))
                {
                     _logger.Information("Setting player name for instance '{InstanceName}' to '{NewPlayerName}'.", instance.Name, playerName);
                    instance.PlayerName = playerName;
                    await SaveInstanceAsync(instance);
                }
                _logger.Information("Loaded existing instance: '{InstanceName}'", instance.Name);
                return instance;
            }
            return await CreateInstanceAsync(name, minecraftVersionId, playerName);
        }


        public async Task<Instance> LoadInstanceAsync(string name) 
        {
            string instancePath = GetInstancePath(name); 
            string metadataFilePath = Path.Combine(instancePath, InstanceMetadataFileName);

            if (!File.Exists(metadataFilePath))
            {
                _logger.Verbose("Instance metadata file not found for '{InstanceName}' at {FilePath}. Assuming instance does not exist or is incomplete.", name, metadataFilePath);
                return null;
            }

            try
            {
                string json = await File.ReadAllTextAsync(metadataFilePath);
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

            string metadataFilePath = Path.Combine(instance.InstancePath, InstanceMetadataFileName);
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                };
                string json = JsonSerializer.Serialize(instance, options);
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
                string instanceDirName = Path.GetFileName(dir); 
                var instance = await LoadInstanceAsync(instanceDirName); 
                if (instance != null)
                {
                    instances.Add(instance);
                }
            }
            _logger.Information("Loaded {Count} instances from {InstancesRootDir}", instances.Count, _launcherConfig.InstancesRootDir);
            return instances;
        }

        public async Task UpdateLastPlayedAsync(Instance instance, TimeSpan sessionDuration)
        {
            if (instance == null) return;
            instance.LastPlayedDate = DateTime.UtcNow;
            instance.LastSessionPlaytime = sessionDuration;
            instance.TotalPlaytime += sessionDuration; // Accumulate total playtime
            await SaveInstanceAsync(instance);
            _logger.Information("Updated playtime for instance '{InstanceName}'. Last Session: {LastSessionPlaytime}, Total Playtime: {TotalPlaytime}", 
                instance.Name, instance.LastSessionPlaytime, instance.TotalPlaytime);
        }
    }
}