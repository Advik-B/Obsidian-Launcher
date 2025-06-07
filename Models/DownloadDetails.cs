using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models
{
    /// <summary>
    /// Represents the download details for a specific file, such as a Minecraft client/server JAR or mappings.
    /// Corresponds to the objects within the "downloads" section of the Minecraft version manifest.
    /// </summary>
    public class DownloadDetails
    {
        /// <summary>
        /// The SHA1 checksum of the downloadable file.
        /// </summary>
        [JsonPropertyName("sha1")]
        public string Sha1 { get; set; }

        /// <summary>
        /// The size of the downloadable file in bytes.
        /// </summary>
        [JsonPropertyName("size")]
        public uint Size { get; set; } // C++ used unsigned int, so uint is appropriate

        /// <summary>
        /// The URL from which the file can be downloaded.
        /// </summary>
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}