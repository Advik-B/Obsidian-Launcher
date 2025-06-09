using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

/// <summary>
///     Represents operating system specific conditions or information,
///     typically used within rules for libraries or arguments.
///     Corresponds to the "os" object in Minecraft version manifest rules.
/// </summary>
public class OperatingSystemInfo
{
    /// <summary>
    ///     The name of the operating system (e.g., "windows", "osx", "linux").
    ///     This property might be null if not specified in a rule, meaning the rule applies to any OS
    ///     (unless other OS-specific rules exist).
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    ///     An optional regular expression pattern to match against the operating system version.
    ///     Example: "^10\\." for Windows 10.
    ///     This property will be null if no version constraint is specified.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; } // Nullable if "version" is not present

    /// <summary>
    ///     The specific processor architecture (e.g., "x86", "x64", "arm64").
    ///     This is typically used in JVM argument rules.
    ///     This property will be null if no architecture constraint is specified.
    /// </summary>
    [JsonPropertyName("arch")]
    public string? Arch { get; set; } // Nullable if "arch" is not present
}