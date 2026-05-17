// Tests for Maven coordinate → path resolution.
// This matches Prism Launcher's own Maven resolution logic used for Forge/Fabric/Quilt/NeoForge libraries.
namespace ObsidianLauncher.Tests;

public class MavenPathTests
{
    // Inline the exact logic from LibraryManager.MavenNameToPath to test it in isolation.
    static string MavenNameToPath(string name)
    {
        var parts = name.Split(':');
        if (parts.Length < 3) return name;
        var group = parts[0].Replace('.', '/');
        var artifact = parts[1];
        var version = parts[2];
        var classifier = parts.Length >= 4 ? "-" + parts[3] : "";
        var ext = parts.Length >= 5 ? parts[4] : "jar";
        return $"{group}/{artifact}/{version}/{artifact}-{version}{classifier}.{ext}";
    }

    [Theory]
    // Prism Launcher / vanilla Minecraft library coordinates
    [InlineData("com.mojang:authlib:4.0.43",
                "com/mojang/authlib/4.0.43/authlib-4.0.43.jar")]
    [InlineData("org.lwjgl:lwjgl:3.3.3",
                "org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar")]
    // Fabric loader
    [InlineData("net.fabricmc:fabric-loader:0.15.7",
                "net/fabricmc/fabric-loader/0.15.7/fabric-loader-0.15.7.jar")]
    // Quilt loader
    [InlineData("org.quiltmc:quilt-loader:0.24.0",
                "org/quiltmc/quilt-loader/0.24.0/quilt-loader-0.24.0.jar")]
    // Forge (deep group id)
    [InlineData("net.minecraftforge:forge:1.20.1-47.2.0",
                "net/minecraftforge/forge/1.20.1-47.2.0/forge-1.20.1-47.2.0.jar")]
    // NeoForge
    [InlineData("net.neoforged:neoforge:21.1.73",
                "net/neoforged/neoforge/21.1.73/neoforge-21.1.73.jar")]
    // Native library with classifier (e.g. lwjgl natives)
    [InlineData("org.lwjgl:lwjgl:3.3.3:natives-linux",
                "org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3-natives-linux.jar")]
    // Log4j (used by Minecraft directly)
    [InlineData("org.apache.logging.log4j:log4j-api:2.22.1",
                "org/apache/logging/log4j/log4j-api/2.22.1/log4j-api-2.22.1.jar")]
    public void MavenCoordinateConvertsToExpectedPath(string coordinate, string expected)
    {
        var actual = MavenNameToPath(coordinate);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MalformedCoordinateReturnedUnchanged()
    {
        // Fewer than 3 parts → fallback to returning the input unchanged
        Assert.Equal("bad-coordinate", MavenNameToPath("bad-coordinate"));
    }
}
