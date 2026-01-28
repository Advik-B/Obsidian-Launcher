// Utils/ConfigVersioning.cs

using System;
using System.IO;
using System.Text.Json;
using Serilog;

namespace ObsidianLauncher.Utils;

/// <summary>
///     Configuration versioning utility to handle config file migrations and compatibility.
/// </summary>
public static class ConfigVersioning
{
    private static readonly ILogger _logger = Log.ForContext(typeof(ConfigVersioning));

    /// <summary>
    ///     Metadata for configuration file versioning.
    /// </summary>
    public class ConfigMetadata
    {
        /// <summary>
        ///     Configuration format version.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        ///     Application version that created this config.
        /// </summary>
        public string? AppVersion { get; set; }

        /// <summary>
        ///     Timestamp when config was created.
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        ///     Timestamp when config was last modified.
        /// </summary>
        public DateTime Modified { get; set; }

        /// <summary>
        ///     Config file format (e.g., "toml", "json").
        /// </summary>
        public string Format { get; set; } = "toml";
    }

    /// <summary>
    ///     Creates a metadata file for a configuration file.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="version">Configuration version number.</param>
    /// <param name="appVersion">Application version.</param>
    /// <param name="format">Configuration file format.</param>
    public static void CreateMetadata(string configPath, int version, string? appVersion = null, string format = "toml")
    {
        try
        {
            var metadataPath = configPath + ".meta";
            var metadata = new ConfigMetadata
            {
                Version = version,
                AppVersion = appVersion ?? GetApplicationVersion(),
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
                Format = format
            };

            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metadataPath, json);

            _logger.Information("Created config metadata: {Path} (version {Version})", metadataPath, version);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create config metadata for: {Path}", configPath);
        }
    }

    /// <summary>
    ///     Reads metadata for a configuration file.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <returns>Configuration metadata, or null if not found.</returns>
    public static ConfigMetadata? ReadMetadata(string configPath)
    {
        try
        {
            var metadataPath = configPath + ".meta";
            
            if (!File.Exists(metadataPath))
            {
                _logger.Debug("No metadata file found for: {Path}", configPath);
                return null;
            }

            var json = File.ReadAllText(metadataPath);
            var metadata = JsonSerializer.Deserialize<ConfigMetadata>(json);

            _logger.Debug("Read config metadata: {Path} (version {Version})", metadataPath, metadata?.Version);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to read config metadata for: {Path}", configPath);
            return null;
        }
    }

    /// <summary>
    ///     Updates the metadata file for a configuration file.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="version">New version number (null to keep existing).</param>
    public static void UpdateMetadata(string configPath, int? version = null)
    {
        try
        {
            var metadata = ReadMetadata(configPath);
            
            if (metadata == null)
            {
                // Create new metadata
                CreateMetadata(configPath, version ?? 1);
                return;
            }

            if (version.HasValue)
            {
                metadata.Version = version.Value;
            }

            metadata.Modified = DateTime.UtcNow;

            var metadataPath = configPath + ".meta";
            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metadataPath, json);

            _logger.Information("Updated config metadata: {Path} (version {Version})", metadataPath, metadata.Version);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update config metadata for: {Path}", configPath);
        }
    }

    /// <summary>
    ///     Backs up a configuration file before migration.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <returns>Path to the backup file, or null on failure.</returns>
    public static string? BackupConfig(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                _logger.Warning("Config file does not exist for backup: {Path}", configPath);
                return null;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = $"{configPath}.backup_{timestamp}";

            File.Copy(configPath, backupPath, overwrite: false);
            _logger.Information("Backed up config file: {Original} -> {Backup}", configPath, backupPath);

            return backupPath;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to backup config file: {Path}", configPath);
            return null;
        }
    }

    /// <summary>
    ///     Checks if a configuration file needs migration based on version.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="currentVersion">Current expected version.</param>
    /// <returns>True if migration is needed.</returns>
    public static bool NeedsMigration(string configPath, int currentVersion)
    {
        var metadata = ReadMetadata(configPath);
        
        if (metadata == null)
        {
            _logger.Information("Config has no metadata, assuming needs migration: {Path}", configPath);
            return true;
        }

        var needsMigration = metadata.Version < currentVersion;
        
        if (needsMigration)
        {
            _logger.Information("Config needs migration: {Path} (version {OldVersion} -> {NewVersion})", 
                configPath, metadata.Version, currentVersion);
        }

        return needsMigration;
    }

    /// <summary>
    ///     Restores a configuration file from backup.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="backupPath">Path to the backup file.</param>
    /// <returns>True if restore was successful.</returns>
    public static bool RestoreFromBackup(string configPath, string backupPath)
    {
        try
        {
            if (!File.Exists(backupPath))
            {
                _logger.Error("Backup file does not exist: {Path}", backupPath);
                return false;
            }

            File.Copy(backupPath, configPath, overwrite: true);
            _logger.Information("Restored config from backup: {Backup} -> {Config}", backupPath, configPath);

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to restore config from backup: {Backup}", backupPath);
            return false;
        }
    }

    /// <summary>
    ///     Gets the current application version.
    /// </summary>
    /// <returns>Application version string.</returns>
    private static string GetApplicationVersion()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    ///     Validates configuration file structure.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <returns>True if configuration is valid.</returns>
    public static bool ValidateConfig(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                _logger.Warning("Config file does not exist: {Path}", configPath);
                return false;
            }

            // Basic validation: check if file is readable
            var content = File.ReadAllText(configPath);
            
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.Warning("Config file is empty: {Path}", configPath);
                return false;
            }

            _logger.Debug("Config file validated: {Path}", configPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Config file validation failed: {Path}", configPath);
            return false;
        }
    }
}
