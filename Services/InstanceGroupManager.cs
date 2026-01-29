// Services/InstanceGroupManager.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services;

/// <summary>
///     Manages instance groups for organization and categorization.
/// </summary>
public class InstanceGroupManager
{
    private const string GroupsFileName = "groups.json";
    private readonly LauncherConfig _launcherConfig;
    private readonly ILogger _logger;
    private readonly Dictionary<string, InstanceGroup> _groups;
    private readonly string _groupsFilePath;

    public InstanceGroupManager(LauncherConfig launcherConfig)
    {
        _launcherConfig = launcherConfig ?? throw new ArgumentNullException(nameof(launcherConfig));
        _logger = LogHelper.GetLogger<InstanceGroupManager>();
        _groups = new Dictionary<string, InstanceGroup>();
        _groupsFilePath = Path.Combine(_launcherConfig.BaseDataPath, GroupsFileName);

        LoadGroups();
    }

    /// <summary>
    ///     Gets all groups.
    /// </summary>
    public IReadOnlyList<InstanceGroup> GetAllGroups()
    {
        return _groups.Values.OrderBy(g => g.SortOrder).ThenBy(g => g.Name).ToList();
    }

    /// <summary>
    ///     Gets a group by ID.
    /// </summary>
    public InstanceGroup? GetGroup(string groupId)
    {
        return _groups.TryGetValue(groupId, out var group) ? group : null;
    }

    /// <summary>
    ///     Creates a new group.
    /// </summary>
    public InstanceGroup CreateGroup(string name, string? description = null, string? colorCode = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Group name cannot be empty.", nameof(name));

        var group = new InstanceGroup
        {
            Name = name,
            Description = description,
            ColorCode = colorCode,
            SortOrder = _groups.Count
        };

        _groups[group.Id] = group;
        SaveGroups();

        _logger.Information("Created instance group: {GroupName} (ID: {GroupId})", name, group.Id);
        return group;
    }

    /// <summary>
    ///     Updates an existing group.
    /// </summary>
    public bool UpdateGroup(string groupId, Action<InstanceGroup> updateAction)
    {
        if (!_groups.TryGetValue(groupId, out var group))
        {
            _logger.Warning("Group not found for update: {GroupId}", groupId);
            return false;
        }

        updateAction(group);
        SaveGroups();

        _logger.Information("Updated instance group: {GroupName} (ID: {GroupId})", group.Name, groupId);
        return true;
    }

    /// <summary>
    ///     Deletes a group. Instances in the group will have their GroupId set to null.
    /// </summary>
    public bool DeleteGroup(string groupId)
    {
        if (!_groups.Remove(groupId, out var group))
        {
            _logger.Warning("Group not found for deletion: {GroupId}", groupId);
            return false;
        }

        SaveGroups();

        _logger.Information("Deleted instance group: {GroupName} (ID: {GroupId})", group.Name, groupId);
        return true;
    }

    /// <summary>
    ///     Adds an instance to a group.
    /// </summary>
    public bool AddInstanceToGroup(string groupId, string instanceId)
    {
        if (!_groups.TryGetValue(groupId, out var group))
        {
            _logger.Warning("Group not found: {GroupId}", groupId);
            return false;
        }

        if (!group.InstanceIds.Contains(instanceId))
        {
            group.InstanceIds.Add(instanceId);
            SaveGroups();
            _logger.Debug("Added instance {InstanceId} to group {GroupName}", instanceId, group.Name);
        }

        return true;
    }

    /// <summary>
    ///     Removes an instance from a group.
    /// </summary>
    public bool RemoveInstanceFromGroup(string groupId, string instanceId)
    {
        if (!_groups.TryGetValue(groupId, out var group))
        {
            _logger.Warning("Group not found: {GroupId}", groupId);
            return false;
        }

        if (group.InstanceIds.Remove(instanceId))
        {
            SaveGroups();
            _logger.Debug("Removed instance {InstanceId} from group {GroupName}", instanceId, group.Name);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Gets the group an instance belongs to.
    /// </summary>
    public InstanceGroup? GetGroupForInstance(string instanceId)
    {
        return _groups.Values.FirstOrDefault(g => g.InstanceIds.Contains(instanceId));
    }

    /// <summary>
    ///     Gets all instances in a group.
    /// </summary>
    public List<string> GetInstancesInGroup(string groupId)
    {
        return _groups.TryGetValue(groupId, out var group)
            ? new List<string>(group.InstanceIds)
            : new List<string>();
    }

    /// <summary>
    ///     Reorders groups.
    /// </summary>
    public void ReorderGroups(List<string> orderedGroupIds)
    {
        for (int i = 0; i < orderedGroupIds.Count; i++)
        {
            if (_groups.TryGetValue(orderedGroupIds[i], out var group))
            {
                group.SortOrder = i;
            }
        }

        SaveGroups();
        _logger.Debug("Reordered {Count} groups", orderedGroupIds.Count);
    }

    /// <summary>
    ///     Loads groups from disk.
    /// </summary>
    private void LoadGroups()
    {
        try
        {
            if (!File.Exists(_groupsFilePath))
            {
                _logger.Information("Groups file not found, starting with empty group list: {FilePath}", _groupsFilePath);
                return;
            }

            var json = File.ReadAllText(_groupsFilePath);
            var groupsList = JsonSerializer.Deserialize<List<InstanceGroup>>(json);

            if (groupsList != null)
            {
                _groups.Clear();
                foreach (var group in groupsList)
                {
                    _groups[group.Id] = group;
                }

                _logger.Information("Loaded {Count} instance groups from {FilePath}", _groups.Count, _groupsFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load instance groups from {FilePath}", _groupsFilePath);
        }
    }

    /// <summary>
    ///     Saves groups to disk.
    /// </summary>
    private void SaveGroups()
    {
        try
        {
            var groupsList = _groups.Values.ToList();
            var json = JsonSerializer.Serialize(groupsList, new JsonSerializerOptions { WriteIndented = true });

            var directory = Path.GetDirectoryName(_groupsFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_groupsFilePath, json);
            _logger.Debug("Saved {Count} instance groups to {FilePath}", _groups.Count, _groupsFilePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save instance groups to {FilePath}", _groupsFilePath);
        }
    }
}
