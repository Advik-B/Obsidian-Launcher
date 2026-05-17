using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

/// <summary>
///     Represents the details of a downloadable logging configuration file.
/// </summary>
public class LoggingFile
{
    /// <summary>
    ///     The identifier for this logging file configuration (e.g., "client-1.12.xml").
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    ///     The SHA1 checksum of the logging file.
    /// </summary>
    [JsonPropertyName("sha1")]
    public required string Sha1 { get; set; }

    /// <summary>
    ///     The size of the logging file in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public ulong Size { get; set; }

    /// <summary>
    ///     The URL from which the logging file can be downloaded.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }
}