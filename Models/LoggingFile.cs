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
    public string Id { get; set; }

    /// <summary>
    ///     The SHA1 checksum of the logging file.
    /// </summary>
    [JsonPropertyName("sha1")]
    public string Sha1 { get; set; }

    /// <summary>
    ///     The size of the logging file in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public ulong Size { get; set; } // Using ulong as C++ used size_t, which can be 64-bit. uint might also be okay.

    /// <summary>
    ///     The URL from which the logging file can be downloaded.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; }
}