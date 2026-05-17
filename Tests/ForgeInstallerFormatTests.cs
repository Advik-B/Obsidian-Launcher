// Tests for Forge installer format detection (old vs. new format).
// Prism Launcher performs the exact same format check via ForgeInstallProfile.IsNewFormat.
// Old format (≤1.12.2): install_profile.json only, contains "versionInfo" directly.
// New format (≥1.13):   install_profile.json + version.json, contains "processors" list.
using System.Text.Json;
using System.Text.Json.Serialization;
namespace ObsidianLauncher.Tests;

public class ForgeInstallerFormatTests
{
    // Minimal ForgeInstallProfile mirroring Models/Forge/ForgeInstallProfile.cs
    class ForgeInstallProfile
    {
        [JsonPropertyName("spec")]        public int? Spec { get; set; }
        [JsonPropertyName("profile")]     public string? Profile { get; set; }
        [JsonPropertyName("processors")]  public List<object>? Processors { get; set; }
        [JsonPropertyName("versionInfo")] public object? VersionInfo { get; set; }

        public bool IsNewFormat => Spec != null || (Profile != null && Processors != null);
    }

    static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void OldFormatWithVersionInfoIsNotNewFormat()
    {
        const string json = """
            {
              "install": { "target": "1.12.2-forge-14.23.5.2860" },
              "versionInfo": { "id": "1.12.2-forge-14.23.5.2860" }
            }
            """;
        var profile = JsonSerializer.Deserialize<ForgeInstallProfile>(json, Opts)!;
        Assert.False(profile.IsNewFormat);
        Assert.NotNull(profile.VersionInfo);
    }

    [Fact]
    public void NewFormatWithSpecIsNewFormat()
    {
        const string json = """
            {
              "spec": 0,
              "profile": "forge",
              "version": "1.20.1-47.2.0",
              "processors": []
            }
            """;
        var profile = JsonSerializer.Deserialize<ForgeInstallProfile>(json, Opts)!;
        Assert.True(profile.IsNewFormat);
        Assert.Equal(0, profile.Spec);
    }

    [Fact]
    public void NewFormatWithProfileAndProcessorsIsNewFormat()
    {
        const string json = """
            {
              "profile": "forge",
              "processors": [
                { "jar": "net.minecraftforge:installertools:1.3.0" }
              ]
            }
            """;
        var profile = JsonSerializer.Deserialize<ForgeInstallProfile>(json, Opts)!;
        Assert.True(profile.IsNewFormat);
    }

    [Fact]
    public void EmptyProfileIsNotNewFormat()
    {
        const string json = "{}";
        var profile = JsonSerializer.Deserialize<ForgeInstallProfile>(json, Opts)!;
        Assert.False(profile.IsNewFormat);
    }

    [Theory]
    // Test Forge version string splitting: "mcVersion-forgeVersion"
    [InlineData("1.20.1-47.2.0", "1.20.1", "47.2.0")]
    [InlineData("1.12.2-14.23.5.2860", "1.12.2", "14.23.5.2860")]
    [InlineData("1.7.10-10.13.4.1614-1.7.10", "1.7.10", "10.13.4.1614-1.7.10")]
    public void ForgeVersionStringSplitsCorrectly(string combined, string expectedMc, string expectedForge)
    {
        var dashIdx = combined.IndexOf('-');
        Assert.True(dashIdx > 0, $"No dash found in '{combined}'");
        var mcVersion = combined[..dashIdx];
        var forgeVersion = combined[(dashIdx + 1)..];
        Assert.Equal(expectedMc, mcVersion);
        Assert.Equal(expectedForge, forgeVersion);
    }
}
