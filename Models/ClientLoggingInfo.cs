using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

/// <summary>
///     Represents the client-side logging configuration details.
/// </summary>
public class ClientLoggingInfo
{
    /// <summary>
    ///     The command-line argument to pass to the JVM to enable this logging configuration.
    ///     Example: "-Dlog4j.configurationFile=${path}"
    /// </summary>
    [JsonPropertyName("argument")]
    public string Argument { get; set; }

    /// <summary>
    ///     Details of the logging configuration file to download.
    /// </summary>
    [JsonPropertyName("file")]
    public LoggingFile File { get; set; }

    /// <summary>
    ///     The type of logging system this configuration is for (e.g., "log4j2-xml").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }
}