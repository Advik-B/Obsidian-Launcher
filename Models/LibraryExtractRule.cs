using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models
{
    /// <summary>
    /// Defines rules for extracting a native library, specifying which paths to exclude.
    /// </summary>
    public class LibraryExtractRule
    {
        /// <summary>
        /// A list of paths (relative to the root of the archive) to exclude during extraction.
        /// Paths usually end with a '/' to indicate a directory to exclude.
        /// </summary>
        [JsonPropertyName("exclude")]
        public List<string> Exclude { get; set; }

        public LibraryExtractRule()
        {
            Exclude = new List<string>(); // Initialize to avoid null if "exclude" is missing
        }
    }
}