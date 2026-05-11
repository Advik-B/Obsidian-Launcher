// Models/Instance.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

public class Instance
{
    public Instance()
    {
        Id = Guid.NewGuid().ToString();
        CreationDate = DateTime.UtcNow;
        LastPlayedDate = DateTime.MinValue;
        CustomJvmArguments = new List<string>();
        TotalPlaytime = TimeSpan.Zero;
        LastSessionPlaytime = TimeSpan.Zero;
        Components = new List<Component>();
        Tags = new List<string>();
    }

    public string Id { get; set; } = string.Empty;
    public required string Name { get; set; }

    // Replaced MinecraftVersionId with a list of components
    public List<Component> Components { get; set; }

    [JsonIgnore] public required string InstancePath { get; set; }

    [JsonIgnore] public string NativesPath => Path.Combine(InstancePath, "natives");

    [JsonIgnore] public string GameDataPath => InstancePath;

    public string CustomJavaRuntimePath { get; set; } = string.Empty;

    public List<string> CustomJvmArguments { get; set; }

    public DateTime CreationDate { get; set; }
    public DateTime LastPlayedDate { get; set; }

    public TimeSpan TotalPlaytime { get; set; }
    public TimeSpan LastSessionPlaytime { get; set; }

    public string CustomIconPath { get; set; } = string.Empty;

    /// <summary>
    ///     Optional group ID this instance belongs to.
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    ///     Optional notes/description for this instance.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    ///     Tags for categorization and filtering.
    /// </summary>
    public List<string> Tags { get; set; }

    /// <summary>
    ///     Whether this instance is marked as favorite.
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    ///     Last modified date for tracking changes.
    /// </summary>
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Optional author/creator of this instance.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    ///     Sort order for custom ordering (lower numbers appear first).
    /// </summary>
    public int SortOrder { get; set; }

    // Launch pipeline enhancements
    public string? PreLaunchCommand { get; set; }
    public string? PostLaunchCommand { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public string? WrapperCommand { get; set; }

    // Quick play
    public string? QuickPlayServer { get; set; }
    public string? QuickPlayWorld { get; set; }
}