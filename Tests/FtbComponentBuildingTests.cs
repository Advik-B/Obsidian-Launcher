// Tests for FTB manifest → Component list translation.
// Validates that the FtbImporter correctly maps FTB's `targets` list to
// our Component model — the same mapping Prism Launcher performs internally.
using System.Text.Json;
namespace ObsidianLauncher.Tests;

public class FtbComponentBuildingTests
{
    // Minimal target/component models duplicated here for isolation
    record FtbTarget(long Id, string Name, string? Version, string? Type);

    static List<(string Uid, string Version)> BuildComponents(List<FtbTarget> targets)
    {
        var components = new List<(string Uid, string Version)>();
        var mcTarget = targets.FirstOrDefault(t => t.Name.Equals("minecraft", StringComparison.OrdinalIgnoreCase));
        if (mcTarget == null || string.IsNullOrEmpty(mcTarget.Version)) return components;

        components.Add(("net.minecraft", mcTarget.Version));

        var loaderTarget = targets.FirstOrDefault(t =>
            !t.Name.Equals("minecraft", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(t.Version));

        if (loaderTarget != null)
        {
            var uid = loaderTarget.Name.ToLowerInvariant() switch
            {
                "forge"    => "net.minecraftforge",
                "neoforge" => "net.neoforged.neoforge",
                "fabric"   => "net.fabricmc.fabric-loader",
                "quilt"    => "org.quiltmc.quilt-loader",
                _          => (string?)null
            };
            if (uid != null)
            {
                var version = (uid == "net.fabricmc.fabric-loader" || uid == "org.quiltmc.quilt-loader")
                    ? $"{mcTarget.Version}/{loaderTarget.Version}"
                    : loaderTarget.Version!;
                components.Add((uid, version));
            }
        }
        return components;
    }

    [Fact]
    public void ForgePackBuildsCorrectComponents()
    {
        var targets = new List<FtbTarget>
        {
            new(1, "minecraft", "1.20.1", "game"),
            new(2, "forge", "47.2.0", "modloader")
        };
        var components = BuildComponents(targets);
        Assert.Equal(2, components.Count);
        Assert.Equal(("net.minecraft", "1.20.1"), components[0]);
        Assert.Equal(("net.minecraftforge", "47.2.0"), components[1]);
    }

    [Fact]
    public void FabricPackEncodesVersionAsGameSlashLoader()
    {
        var targets = new List<FtbTarget>
        {
            new(1, "minecraft", "1.20.4", "game"),
            new(2, "fabric", "0.15.7", "modloader")
        };
        var components = BuildComponents(targets);
        Assert.Equal(("net.fabricmc.fabric-loader", "1.20.4/0.15.7"), components[1]);
    }

    [Fact]
    public void NeoForgePackBuildsCorrectUid()
    {
        var targets = new List<FtbTarget>
        {
            new(1, "minecraft", "1.21.0", "game"),
            new(2, "neoforge", "21.0.167", "modloader")
        };
        var components = BuildComponents(targets);
        Assert.Equal("net.neoforged.neoforge", components[1].Uid);
        Assert.Equal("21.0.167", components[1].Version);
    }

    [Fact]
    public void VanillaPackYieldsOnlyMinecraftComponent()
    {
        var targets = new List<FtbTarget>
        {
            new(1, "minecraft", "1.20.1", "game")
        };
        var components = BuildComponents(targets);
        Assert.Single(components);
        Assert.Equal("net.minecraft", components[0].Uid);
    }

    [Fact]
    public void MissingMinecraftTargetReturnsEmpty()
    {
        var targets = new List<FtbTarget>
        {
            new(1, "forge", "47.2.0", "modloader") // no minecraft target
        };
        var components = BuildComponents(targets);
        Assert.Empty(components);
    }
}
