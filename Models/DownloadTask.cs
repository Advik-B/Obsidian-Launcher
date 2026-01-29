// Models/DownloadTask.cs

using System;
using System.Threading;

namespace ObsidianLauncher.Models;

/// <summary>
///     Represents a download task with priority and status tracking.
/// </summary>
public class DownloadTask
{
    public DownloadTask(string url, string filePath, int priority = 0)
    {
        Id = Guid.NewGuid().ToString();
        Url = url ?? throw new ArgumentNullException(nameof(url));
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        Priority = priority;
        Status = DownloadStatus.Queued;
        CreatedAt = DateTime.UtcNow;
    }

    public string Id { get; set; }
    public string Url { get; set; }
    public string FilePath { get; set; }
    public int Priority { get; set; }
    public DownloadStatus Status { get; set; }
    public long TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public double ProgressPercent => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100 : 0;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public CancellationTokenSource? CancellationSource { get; set; }

    /// <summary>
    ///     Expected SHA1 hash for verification (optional).
    /// </summary>
    public string? ExpectedSha1 { get; set; }

    /// <summary>
    ///     Whether to verify the download after completion.
    /// </summary>
    public bool VerifyHash { get; set; }

    /// <summary>
    ///     Number of retry attempts made.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    ///     Maximum number of retries allowed.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
///     Download status enumeration.
/// </summary>
public enum DownloadStatus
{
    Queued,
    Downloading,
    Completed,
    Failed,
    Cancelled,
    Verifying,
    Paused
}
