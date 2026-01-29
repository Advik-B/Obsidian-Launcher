// Utils/UuidUtils.cs

using System;
using Serilog;

namespace ObsidianLauncher.Utils;

/// <summary>
///     Utility class for UUID/GUID generation and manipulation.
/// </summary>
public static class UuidUtils
{
    private static readonly ILogger _logger = Log.ForContext(typeof(UuidUtils));

    /// <summary>
    ///     Generates a new random UUID (GUID v4).
    /// </summary>
    /// <returns>A new UUID as a string.</returns>
    public static string GenerateUuid()
    {
        var uuid = Guid.NewGuid().ToString();
        _logger.Verbose("Generated UUID: {Uuid}", uuid);
        return uuid;
    }

    /// <summary>
    ///     Generates a new random UUID without hyphens.
    /// </summary>
    /// <returns>A new UUID as a string without hyphens.</returns>
    public static string GenerateUuidWithoutHyphens()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    ///     Generates a UUID from a namespace and name (UUID v5 with SHA1).
    ///     This produces deterministic UUIDs - same input always gives same output.
    /// </summary>
    /// <param name="namespaceUuid">The namespace UUID.</param>
    /// <param name="name">The name to hash.</param>
    /// <returns>A deterministic UUID based on namespace and name.</returns>
    public static string GenerateUuidV5(Guid namespaceUuid, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name cannot be null or empty", nameof(name));
        }

        // UUID v5 uses SHA1 hashing
        using var sha1 = System.Security.Cryptography.SHA1.Create();

        // Combine namespace UUID bytes with name bytes
        var namespaceBytes = namespaceUuid.ToByteArray();
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        var combined = new byte[namespaceBytes.Length + nameBytes.Length];

        Buffer.BlockCopy(namespaceBytes, 0, combined, 0, namespaceBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, combined, namespaceBytes.Length, nameBytes.Length);

        // Hash the combined bytes
        var hash = sha1.ComputeHash(combined);

        // Take first 16 bytes
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);

        // Set version to 5
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);

        // Set variant to RFC 4122
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        var uuid = new Guid(guidBytes).ToString();
        _logger.Verbose("Generated UUID v5 from namespace {Namespace} and name {Name}: {Uuid}",
            namespaceUuid, name, uuid);

        return uuid;
    }

    /// <summary>
    ///     Generates a player UUID for Minecraft offline mode.
    ///     Uses UUID v3 (MD5) with the Minecraft namespace.
    /// </summary>
    /// <param name="playerName">The player name.</param>
    /// <returns>A UUID for the offline player.</returns>
    public static string GenerateOfflinePlayerUuid(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            throw new ArgumentException("Player name cannot be null or empty", nameof(playerName));
        }

        // Minecraft uses a specific namespace for offline UUIDs
        var minecraftNamespace = "OfflinePlayer:";
        var name = minecraftNamespace + playerName;

        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(name));

        // Set version to 3 (MD5)
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);

        // Set variant to RFC 4122
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        var uuid = new Guid(hash).ToString();
        _logger.Debug("Generated offline player UUID for {PlayerName}: {Uuid}", playerName, uuid);

        return uuid;
    }

    /// <summary>
    ///     Validates if a string is a valid UUID format.
    /// </summary>
    /// <param name="uuid">The UUID string to validate.</param>
    /// <returns>True if valid UUID format, false otherwise.</returns>
    public static bool IsValidUuid(string? uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return false;

        return Guid.TryParse(uuid, out _);
    }

    /// <summary>
    ///     Parses a UUID string to a Guid.
    /// </summary>
    /// <param name="uuid">The UUID string.</param>
    /// <returns>The parsed Guid, or null if invalid.</returns>
    public static Guid? ParseUuid(string? uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return null;

        if (Guid.TryParse(uuid, out var guid))
            return guid;

        return null;
    }

    /// <summary>
    ///     Formats a UUID to a specific format.
    /// </summary>
    /// <param name="uuid">The UUID to format.</param>
    /// <param name="format">The format: N (no hyphens), D (with hyphens), B (with braces), P (with parentheses).</param>
    /// <returns>The formatted UUID string.</returns>
    public static string FormatUuid(Guid uuid, string format = "D")
    {
        return uuid.ToString(format);
    }
}
