// Utils/LogRotation.cs

using System;
using System.IO;
using System.Linq;
using Serilog;

namespace ObsidianLauncher.Utils;

/// <summary>
///     Utility for log file rotation to prevent unlimited disk usage.
/// </summary>
public static class LogRotation
{
    private static readonly ILogger _logger = Log.ForContext(typeof(LogRotation));

    /// <summary>
    ///     Rotates log files in a directory, keeping only the specified number of files.
    /// </summary>
    /// <param name="logDirectory">The directory containing log files.</param>
    /// <param name="logFilePattern">The log file pattern (e.g., "*.log").</param>
    /// <param name="maxFiles">Maximum number of log files to keep.</param>
    /// <param name="maxSizeBytes">Maximum total size of all log files in bytes (0 for no limit).</param>
    public static void RotateLogFiles(string logDirectory, string logFilePattern = "*.log", int maxFiles = 10, long maxSizeBytes = 0)
    {
        try
        {
            if (!Directory.Exists(logDirectory))
            {
                _logger.Warning("Log directory does not exist: {Directory}", logDirectory);
                return;
            }

            var logFiles = Directory.GetFiles(logDirectory, logFilePattern)
                                   .Select(f => new FileInfo(f))
                                   .OrderByDescending(f => f.LastWriteTime)
                                   .ToList();

            _logger.Information("Found {Count} log files in {Directory}", logFiles.Count, logDirectory);

            // Delete old files beyond maxFiles limit
            if (logFiles.Count > maxFiles)
            {
                var filesToDelete = logFiles.Skip(maxFiles).ToList();
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        file.Delete();
                        _logger.Information("Deleted old log file: {File}", file.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to delete log file: {File}", file.FullName);
                    }
                }
            }

            // Check total size if maxSizeBytes is specified
            if (maxSizeBytes > 0)
            {
                var remainingFiles = Directory.GetFiles(logDirectory, logFilePattern)
                                             .Select(f => new FileInfo(f))
                                             .OrderByDescending(f => f.LastWriteTime)
                                             .ToList();

                long totalSize = remainingFiles.Sum(f => f.Length);

                if (totalSize > maxSizeBytes)
                {
                    _logger.Information("Total log size {TotalSize} exceeds limit {MaxSize}, deleting oldest files",
                        SystemInfo.FormatByteSize(totalSize), SystemInfo.FormatByteSize(maxSizeBytes));

                    // Delete oldest files until under limit
                    foreach (var file in remainingFiles.Reverse<FileInfo>())
                    {
                        if (totalSize <= maxSizeBytes)
                            break;

                        try
                        {
                            var fileSize = file.Length;
                            file.Delete();
                            totalSize -= fileSize;
                            _logger.Information("Deleted log file to reduce size: {File} ({Size})",
                                file.Name, SystemInfo.FormatByteSize(fileSize));
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning(ex, "Failed to delete log file: {File}", file.FullName);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to rotate log files in {Directory}", logDirectory);
        }
    }

    /// <summary>
    ///     Compresses old log files to save disk space.
    /// </summary>
    /// <param name="logDirectory">The directory containing log files.</param>
    /// <param name="logFilePattern">The log file pattern (e.g., "*.log").</param>
    /// <param name="olderThanDays">Compress files older than this many days.</param>
    public static void CompressOldLogs(string logDirectory, string logFilePattern = "*.log", int olderThanDays = 7)
    {
        try
        {
            if (!Directory.Exists(logDirectory))
            {
                _logger.Warning("Log directory does not exist: {Directory}", logDirectory);
                return;
            }

            var threshold = DateTime.UtcNow.AddDays(-olderThanDays);
            var logFiles = Directory.GetFiles(logDirectory, logFilePattern)
                                   .Select(f => new FileInfo(f))
                                   .Where(f => f.LastWriteTimeUtc < threshold)
                                   .Where(f => !f.Name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                                   .ToList();

            _logger.Information("Found {Count} log files older than {Days} days to compress", logFiles.Count, olderThanDays);

            foreach (var file in logFiles)
            {
                try
                {
                    var gzipPath = file.FullName + ".gz";

                    if (File.Exists(gzipPath))
                    {
                        _logger.Debug("Compressed file already exists: {File}", gzipPath);
                        continue;
                    }

                    using (var inputStream = file.OpenRead())
                    using (var outputStream = File.Create(gzipPath))
                    using (var gzipStream = new System.IO.Compression.GZipStream(outputStream, System.IO.Compression.CompressionLevel.Optimal))
                    {
                        inputStream.CopyTo(gzipStream);
                    }

                    _logger.Information("Compressed log file: {Original} -> {Compressed}",
                        file.Name, Path.GetFileName(gzipPath));

                    // Delete original file after successful compression
                    file.Delete();
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to compress log file: {File}", file.FullName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to compress old log files in {Directory}", logDirectory);
        }
    }

    /// <summary>
    ///     Archives old log files to a separate directory.
    /// </summary>
    /// <param name="logDirectory">The directory containing log files.</param>
    /// <param name="archiveDirectory">The directory to move archived files to.</param>
    /// <param name="logFilePattern">The log file pattern (e.g., "*.log").</param>
    /// <param name="olderThanDays">Archive files older than this many days.</param>
    public static void ArchiveOldLogs(string logDirectory, string archiveDirectory, string logFilePattern = "*.log", int olderThanDays = 30)
    {
        try
        {
            if (!Directory.Exists(logDirectory))
            {
                _logger.Warning("Log directory does not exist: {Directory}", logDirectory);
                return;
            }

            if (!Directory.Exists(archiveDirectory))
            {
                Directory.CreateDirectory(archiveDirectory);
                _logger.Information("Created archive directory: {Directory}", archiveDirectory);
            }

            var threshold = DateTime.UtcNow.AddDays(-olderThanDays);
            var logFiles = Directory.GetFiles(logDirectory, logFilePattern)
                                   .Select(f => new FileInfo(f))
                                   .Where(f => f.LastWriteTimeUtc < threshold)
                                   .ToList();

            _logger.Information("Found {Count} log files older than {Days} days to archive", logFiles.Count, olderThanDays);

            foreach (var file in logFiles)
            {
                try
                {
                    var archivePath = Path.Combine(archiveDirectory, file.Name);

                    if (File.Exists(archivePath))
                    {
                        // Add timestamp to avoid conflicts
                        var timestamp = file.LastWriteTimeUtc.ToString("yyyyMMdd_HHmmss");
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(file.Name);
                        var ext = Path.GetExtension(file.Name);
                        archivePath = Path.Combine(archiveDirectory, $"{nameWithoutExt}_{timestamp}{ext}");
                    }

                    file.MoveTo(archivePath);
                    _logger.Information("Archived log file: {File} -> {Archive}", file.Name, Path.GetFileName(archivePath));
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to archive log file: {File}", file.FullName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to archive old log files from {Directory}", logDirectory);
        }
    }
}
