using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

/// <summary>
///     Contains download information for a library, including its main artifact and any classifiers (like natives).
/// </summary>
public class LibraryDownloads
{
    public LibraryDownloads()
    {
        // Initialize to prevent null reference if "classifiers" is missing in JSON but accessed
        Classifiers = new Dictionary<string, LibraryArtifact>();
    }

    /// <summary>
    ///     The main artifact (usually the JAR file) for the library. Can be null if not specified.
    /// </summary>
    [JsonPropertyName("artifact")]
    public LibraryArtifact? Artifact { get; set; } // Nullable if "artifact" object might be absent

    /// <summary>
    ///     A dictionary of classified artifacts. The key is the classifier name (e.g., "natives-windows", "natives-linux",
    ///     "sources", "javadoc").
    ///     The value provides the download details for that classified artifact. Can be null or empty.
    /// </summary>
    [JsonPropertyName("classifiers")]
    public Dictionary<string, LibraryArtifact>
        Classifiers { get; set; } // Nullable if "classifiers" object might be absent
}