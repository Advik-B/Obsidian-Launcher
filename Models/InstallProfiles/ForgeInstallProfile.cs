using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models.InstallProfiles;

/// <summary>
/// Represents the installation profile found within a Forge installer JAR.
/// This model maps directly to the structure of 'install_profile.json'.
/// </summary>
public class ForgeInstallProfile
{
    /// <summary>
    /// Contains the complete version JSON object for the Forge-patched version.
    /// This is deserialized directly into our existing MinecraftVersion model.
    /// </summary>
    [JsonPropertyName("versionInfo")]
    public MinecraftVersion VersionInfo { get; set; }
}