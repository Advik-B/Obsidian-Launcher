namespace ObsidianLauncher.Models;

/// <summary>
///     Progress report structure for library processing.
/// </summary>
public class LibraryProcessingProgress
{
    public string? CurrentLibraryName { get; set; }
    public int ProcessedLibraries { get; set; }
    public int TotalLibraries { get; set; }

    // e.g., "Downloading", "Verifying", "Extracting", "Skipped"
    public string? Status { get; set; }
}