// Utils/VersionUtils.cs

using System;
using System.Text.RegularExpressions;
using Serilog;

namespace ObsidianLauncher.Utils;

/// <summary>
///     Utility class for parsing and comparing version numbers.
///     Supports semantic versioning (SemVer) and Minecraft-style versions.
/// </summary>
public static class VersionUtils
{
    private static readonly ILogger _logger = Log.ForContext(typeof(VersionUtils));

    /// <summary>
    ///     Parses a version string into a Version object.
    ///     Handles various formats including "1.2.3", "1.20.4", "1.2.3-alpha", etc.
    /// </summary>
    /// <param name="versionString">The version string to parse.</param>
    /// <returns>A Version object if successful, null otherwise.</returns>
    public static Version? ParseVersion(string? versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
        {
            _logger.Warning("Attempted to parse null or empty version string");
            return null;
        }

        try
        {
            // Remove any pre-release tags (e.g., "1.2.3-alpha" -> "1.2.3")
            var cleanVersion = Regex.Replace(versionString, @"-.*$", "");
            
            // Handle versions with less than 4 parts by padding with zeros
            var parts = cleanVersion.Split('.');
            if (parts.Length == 2)
            {
                cleanVersion += ".0.0";
            }
            else if (parts.Length == 3)
            {
                cleanVersion += ".0";
            }

            return new Version(cleanVersion);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to parse version string: {VersionString}", versionString);
            return null;
        }
    }

    /// <summary>
    ///     Compares two version strings.
    /// </summary>
    /// <param name="version1">First version string.</param>
    /// <param name="version2">Second version string.</param>
    /// <returns>
    ///     -1 if version1 &lt; version2,
    ///     0 if version1 == version2,
    ///     1 if version1 &gt; version2,
    ///     null if either version is invalid.
    /// </returns>
    public static int? CompareVersions(string? version1, string? version2)
    {
        var v1 = ParseVersion(version1);
        var v2 = ParseVersion(version2);

        if (v1 == null || v2 == null)
        {
            _logger.Warning("Cannot compare versions: v1={V1}, v2={V2}", version1, version2);
            return null;
        }

        return v1.CompareTo(v2);
    }

    /// <summary>
    ///     Checks if version1 is newer than version2.
    /// </summary>
    /// <param name="version1">First version string.</param>
    /// <param name="version2">Second version string.</param>
    /// <returns>True if version1 is newer, false otherwise.</returns>
    public static bool IsNewer(string? version1, string? version2)
    {
        var result = CompareVersions(version1, version2);
        return result.HasValue && result.Value > 0;
    }

    /// <summary>
    ///     Checks if version1 is older than version2.
    /// </summary>
    /// <param name="version1">First version string.</param>
    /// <param name="version2">Second version string.</param>
    /// <returns>True if version1 is older, false otherwise.</returns>
    public static bool IsOlder(string? version1, string? version2)
    {
        var result = CompareVersions(version1, version2);
        return result.HasValue && result.Value < 0;
    }

    /// <summary>
    ///     Checks if two versions are equal.
    /// </summary>
    /// <param name="version1">First version string.</param>
    /// <param name="version2">Second version string.</param>
    /// <returns>True if versions are equal, false otherwise.</returns>
    public static bool AreEqual(string? version1, string? version2)
    {
        var result = CompareVersions(version1, version2);
        return result.HasValue && result.Value == 0;
    }

    /// <summary>
    ///     Validates if a version string is in a valid format.
    /// </summary>
    /// <param name="versionString">The version string to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidVersion(string? versionString)
    {
        return ParseVersion(versionString) != null;
    }

    /// <summary>
    ///     Gets the major version number from a version string.
    /// </summary>
    /// <param name="versionString">The version string.</param>
    /// <returns>The major version number, or -1 if invalid.</returns>
    public static int GetMajorVersion(string? versionString)
    {
        var version = ParseVersion(versionString);
        return version?.Major ?? -1;
    }

    /// <summary>
    ///     Gets the minor version number from a version string.
    /// </summary>
    /// <param name="versionString">The version string.</param>
    /// <returns>The minor version number, or -1 if invalid.</returns>
    public static int GetMinorVersion(string? versionString)
    {
        var version = ParseVersion(versionString);
        return version?.Minor ?? -1;
    }

    /// <summary>
    ///     Extracts pre-release tag from a version string (e.g., "alpha", "beta", "rc1").
    /// </summary>
    /// <param name="versionString">The version string.</param>
    /// <returns>The pre-release tag, or null if none exists.</returns>
    public static string? GetPreReleaseTag(string? versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return null;

        var match = Regex.Match(versionString, @"-(.+)$");
        return match.Success ? match.Groups[1].Value : null;
    }
}
