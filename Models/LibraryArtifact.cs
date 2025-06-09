using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

/// <summary>
///     Represents a specific artifact (file) associated with a library, like the main JAR or a classifier.
/// </summary>
public class LibraryArtifact
{
    /// <summary>
    ///     The relative path where this artifact should be stored (e.g., "com/example/mylib/1.0/mylib-1.0.jar").
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; set; }

    /// <summary>
    ///     The SHA1 checksum of the artifact file.
    /// </summary>
    [JsonPropertyName("sha1")]
    public required string Sha1 { get; set; }

    /// <summary>
    ///     The size of the artifact file in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public uint Size { get; set; } // C++ used unsigned int

    /// <summary>
    ///     The URL from which this artifact can be downloaded.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }
}