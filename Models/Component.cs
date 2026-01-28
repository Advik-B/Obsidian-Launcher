// Models/Component.cs
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

/// <summary>
/// Represents a single component of an instance, like the base game, a mod loader, or a patch.
/// Inspired by Prism Launcher's component system.
/// </summary>
public class Component
{
    /// <summary>
    /// A unique identifier for the component, often in a Maven-style format.
    /// Examples: "net.minecraft", "net.fabricmc.fabric-loader", "net.minecraftforge".
    /// </summary>
    [JsonPropertyName("uid")]
    public string Uid { get; set; }

    /// <summary>
    /// The specific version of the component.
    /// Examples: "1.20.4", "0.15.7", "49.0.23".
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; }

    /// <summary>
    /// If true, the component is active and will be included in the launch.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// If true, this component is considered a core part of the instance (like the Minecraft version)
    /// and cannot be removed by the user.
    /// </summary>
    [JsonPropertyName("important")]
    public bool IsImportant { get; set; } = false;

    /// <summary>
    /// If true, this component was added automatically to satisfy a dependency and may be
    /// removed if no other components depend on it.
    /// </summary>
    [JsonPropertyName("dependencyOnly")]
    public bool IsDependencyOnly { get; set; } = false;
}