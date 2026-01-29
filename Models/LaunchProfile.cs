// Models/LaunchProfile.cs
using System.Collections.Generic;
using System.Linq;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Models;

/// <summary>
/// Represents the final, merged configuration for launching a specific instance.
/// It is constructed by layering the data from all active components (e.g., Minecraft, Forge, Fabric).
/// </summary>
public class LaunchProfile
{
    private readonly ILogger _logger = LogHelper.GetLogger<LaunchProfile>();

    public string Id { get; set; } = string.Empty;
    public string MainClass { get; set; } = string.Empty;
    public VersionArguments Arguments { get; set; } = new();
    public AssetIndex AssetIndex { get; set; } = null!;
    public string Assets { get; set; } = string.Empty;
    public Dictionary<string, DownloadDetails> Downloads { get; set; } = new();
    public JavaVersionInfo JavaVersion { get; set; } = null!;
    public VersionLogging Logging { get; set; } = null!;
    public List<Library> Libraries { get; set; } = new();
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Merges data from a MinecraftVersion object into this launch profile.
    /// The last-merged component's properties (like mainClass) will typically override previous ones.
    /// </summary>
    public void MergeFrom(MinecraftVersion version)
    {
        _logger.Information("Merging component version '{ComponentId}' into launch profile...", version.Id);
        
        // Overwrite simple properties if they are present in the new version file
        Id = version.Id; // The ID of the final component is usually what we want
        if (!string.IsNullOrEmpty(version.MainClass))
        {
            _logger.Verbose("Setting MainClass to '{MainClass}' from component '{ComponentId}'", version.MainClass, version.Id);
            MainClass = version.MainClass;
        }

        if (!string.IsNullOrEmpty(version.Type))
        {
            Type = version.Type;
        }

        if (version.AssetIndex != null)
        {
            AssetIndex = version.AssetIndex;
        }
        else if (!string.IsNullOrEmpty(version.Assets))
        {
            Assets = version.Assets;
        }
        
        if (version.Downloads != null)
        {
            foreach (var download in version.Downloads)
            {
                Downloads[download.Key] = download.Value;
            }
        }

        if (version.JavaVersion != null)
        {
            JavaVersion = version.JavaVersion;
        }

        if (version.Logging?.Client != null)
        {
            Logging = version.Logging;
        }

        // Merge arguments
        if (version.Arguments?.Jvm != null)
        {
            Arguments.Jvm.AddRange(version.Arguments.Jvm);
        }
        if (version.Arguments?.Game != null)
        {
            Arguments.Game.AddRange(version.Arguments.Game);
        }

        // Merge legacy arguments (less common now, but good for compatibility)
        if (!string.IsNullOrEmpty(version.MinecraftArguments))
        {
            var legacyArgs = version.MinecraftArguments.Split(' ').Select(arg => VersionArgument.Create(arg));
            Arguments.Game.AddRange(legacyArgs);
        }

        // Merge libraries, handling duplicates by replacing older versions
        if (version.Libraries != null)
        {
            var libraryDict = Libraries.ToDictionary(lib => lib.Name, lib => lib);
            foreach (var newLib in version.Libraries)
            {
                if (libraryDict.TryGetValue(newLib.Name, out var existingLib))
                {
                    _logger.Verbose("Library '{LibraryName}' already exists. Deciding whether to update.", newLib.Name);
                    // A simple strategy: assume the new one is better/required.
                    // A more advanced strategy would compare versions if they are parsable.
                    libraryDict[newLib.Name] = newLib; // Replace
                }
                else
                {
                    libraryDict.Add(newLib.Name, newLib);
                }
            }
            Libraries = libraryDict.Values.ToList();
        }
        _logger.Information("Merge complete for component '{ComponentId}'. Library count is now {LibraryCount}.", version.Id, Libraries.Count);
    }
}