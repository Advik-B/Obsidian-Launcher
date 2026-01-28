// Models/ResourceFolder.cs

using System;
using System.Collections.Generic;

namespace ObsidianLauncher.Models;

/// <summary>
///     Represents a type of resource folder (mods, resource packs, shaders, etc.).
/// </summary>
public enum ResourceFolderType
{
    Mods,
    ResourcePacks,
    ShaderPacks,
    TexturePacks,
    DataPacks,
    Worlds,
    Screenshots
}

/// <summary>
///     Represents a resource item in a folder (mod, resource pack, world, etc.).
/// </summary>
public class ResourceItem
{
    public ResourceItem(string path, string name)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    ///     Unique identifier for this resource.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    ///     Full file path to the resource.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    ///     Display name of the resource.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     File size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    ///     Whether this resource is enabled/active.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     Optional description or metadata.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Optional version information.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    ///     Optional author information.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    ///     Tags for categorization.
    /// </summary>
    public List<string> Tags { get; set; } = new List<string>();

    /// <summary>
    ///     Date the resource was added.
    /// </summary>
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Last modified date.
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
