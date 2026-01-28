namespace ObsidianLauncher.Models;

/// <summary>
///     Progress report structure for asset downloads.
/// </summary>
public class AssetDownloadProgress
{
    public string? CurrentFile { get; set; }
    public int ProcessedFiles { get; set; }
    public int TotalFiles { get; set; }
    public long CurrentFileBytesDownloaded { get; set; }
    public long CurrentFileTotalBytes { get; set; }
}