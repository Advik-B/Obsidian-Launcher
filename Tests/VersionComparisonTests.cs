// Tests for semantic version comparison used by the update checker.
// Prism Launcher uses the same pattern: strip pre-release suffixes, compare version quads.
namespace ObsidianLauncher.Tests;

public class VersionComparisonTests
{
    // Inline IsNewerVersion from UpdateChecker
    static bool IsNewerVersion(string latestTag, string currentVersion)
    {
        var latest = latestTag.TrimStart('v');
        var current = currentVersion.TrimStart('v');

        // Strip any pre-release suffix (e.g. "1.2.0-beta.1" → "1.2.0")
        var dashIdx = latest.IndexOf('-');
        if (dashIdx >= 0) latest = latest[..dashIdx];
        dashIdx = current.IndexOf('-');
        if (dashIdx >= 0) current = current[..dashIdx];

        if (Version.TryParse(latest, out var latestVer) && Version.TryParse(current, out var currentVer))
            return latestVer > currentVer;

        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
    }

    [Theory]
    [InlineData("1.2.0", "1.1.0", true)]   // newer minor
    [InlineData("2.0.0", "1.9.9", true)]   // newer major
    [InlineData("1.1.1", "1.1.0", true)]   // newer patch
    [InlineData("1.0.0", "1.0.0", false)]  // same
    [InlineData("1.0.0", "1.1.0", false)]  // older
    [InlineData("v1.2.0", "1.1.0", true)]  // v-prefixed tag
    [InlineData("1.2.0-beta.1", "1.1.0", true)]  // pre-release suffix stripped → 1.2.0 > 1.1.0
    [InlineData("1.1.0-beta.1", "1.1.0", false)] // pre-release stripped → 1.1.0 not > 1.1.0
    public void IsNewerVersionReturnsExpected(string latest, string current, bool expected)
    {
        Assert.Equal(expected, IsNewerVersion(latest, current));
    }
}
