// Models/InstanceGroup.cs

using System;
using System.Collections.Generic;

namespace ObsidianLauncher.Models;

/// <summary>
///     Represents a logical grouping of instances for organization and categorization.
/// </summary>
public class InstanceGroup
{
    public InstanceGroup()
    {
        Id = Guid.NewGuid().ToString();
        InstanceIds = new List<string>();
        CreationDate = DateTime.UtcNow;
    }

    /// <summary>
    ///     Unique identifier for the group.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    ///     Display name of the group.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Optional description for the group.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Optional icon path for the group.
    /// </summary>
    public string? IconPath { get; set; }

    /// <summary>
    ///     List of instance IDs that belong to this group.
    /// </summary>
    public List<string> InstanceIds { get; set; }

    /// <summary>
    ///     Creation date of the group.
    /// </summary>
    public DateTime CreationDate { get; set; }

    /// <summary>
    ///     Optional color code for UI display (e.g., "#FF5733").
    /// </summary>
    public string? ColorCode { get; set; }

    /// <summary>
    ///     Sort order for displaying groups (lower numbers appear first).
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    ///     Whether this group is collapsed in the UI by default.
    /// </summary>
    public bool IsCollapsed { get; set; }
}
