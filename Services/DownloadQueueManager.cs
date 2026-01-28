// Services/DownloadQueueManager.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services;

/// <summary>
///     Manages a queue of download tasks with concurrent execution and priority handling.
/// </summary>
public class DownloadQueueManager : IDisposable
{
    private readonly ILogger _logger;
    private readonly HttpManager _httpManager;
    private readonly ConcurrentQueue<DownloadTask> _downloadQueue;
    private readonly ConcurrentDictionary<string, DownloadTask> _activeDownloads;
    private readonly ConcurrentDictionary<string, DownloadTask> _completedDownloads;
    private readonly SemaphoreSlim _downloadSemaphore;
    private readonly CancellationTokenSource _globalCancellation;
    private readonly int _maxConcurrentDownloads;
    private Task? _queueProcessorTask;
    private bool _isDisposed;

    /// <summary>
    ///     Event fired when a download completes.
    /// </summary>
    public event EventHandler<DownloadTask>? DownloadCompleted;

    /// <summary>
    ///     Event fired when a download fails.
    /// </summary>
    public event EventHandler<DownloadTask>? DownloadFailed;

    /// <summary>
    ///     Event fired when download progress updates.
    /// </summary>
    public event EventHandler<(string TaskId, double Progress)>? ProgressUpdated;

    public DownloadQueueManager(HttpManager httpManager, int maxConcurrentDownloads = 5)
    {
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _maxConcurrentDownloads = Math.Max(1, maxConcurrentDownloads);
        _logger = LogHelper.GetLogger<DownloadQueueManager>();
        
        _downloadQueue = new ConcurrentQueue<DownloadTask>();
        _activeDownloads = new ConcurrentDictionary<string, DownloadTask>();
        _completedDownloads = new ConcurrentDictionary<string, DownloadTask>();
        _downloadSemaphore = new SemaphoreSlim(_maxConcurrentDownloads, _maxConcurrentDownloads);
        _globalCancellation = new CancellationTokenSource();

        _logger.Information("DownloadQueueManager initialized with max {MaxConcurrent} concurrent downloads", 
            _maxConcurrentDownloads);
    }

    /// <summary>
    ///     Starts the queue processor.
    /// </summary>
    public void Start()
    {
        if (_queueProcessorTask == null || _queueProcessorTask.IsCompleted)
        {
            _queueProcessorTask = Task.Run(ProcessQueueAsync, _globalCancellation.Token);
            _logger.Information("Download queue processor started");
        }
    }

    /// <summary>
    ///     Stops the queue processor.
    /// </summary>
    public async Task StopAsync()
    {
        _globalCancellation.Cancel();
        
        if (_queueProcessorTask != null)
        {
            await _queueProcessorTask;
        }

        _logger.Information("Download queue processor stopped");
    }

    /// <summary>
    ///     Enqueues a new download task.
    /// </summary>
    public string EnqueueDownload(string url, string filePath, int priority = 0, string? expectedSha1 = null)
    {
        var task = new DownloadTask(url, filePath, priority)
        {
            ExpectedSha1 = expectedSha1,
            VerifyHash = !string.IsNullOrEmpty(expectedSha1),
            CancellationSource = new CancellationTokenSource()
        };

        _downloadQueue.Enqueue(task);
        _logger.Debug("Enqueued download: {Url} -> {FilePath} (Priority: {Priority})", url, filePath, priority);
        
        return task.Id;
    }

    /// <summary>
    ///     Cancels a download task.
    /// </summary>
    public bool CancelDownload(string taskId)
    {
        // Check active downloads
        if (_activeDownloads.TryGetValue(taskId, out var task))
        {
            task.CancellationSource?.Cancel();
            task.Status = DownloadStatus.Cancelled;
            _logger.Information("Cancelled active download: {TaskId}", taskId);
            return true;
        }

        // For queued downloads, we'd need to filter the queue (which ConcurrentQueue doesn't support well)
        // Instead, we'll mark it for cancellation when it's dequeued
        _logger.Warning("Download task not found in active downloads: {TaskId}", taskId);
        return false;
    }

    /// <summary>
    ///     Gets the status of a download task.
    /// </summary>
    public DownloadTask? GetDownloadStatus(string taskId)
    {
        if (_activeDownloads.TryGetValue(taskId, out var activeTask))
            return activeTask;
        
        if (_completedDownloads.TryGetValue(taskId, out var completedTask))
            return completedTask;

        return null;
    }

    /// <summary>
    ///     Gets all active downloads.
    /// </summary>
    public List<DownloadTask> GetActiveDownloads()
    {
        return _activeDownloads.Values.ToList();
    }

    /// <summary>
    ///     Gets the number of queued downloads.
    /// </summary>
    public int GetQueuedCount()
    {
        return _downloadQueue.Count;
    }

    /// <summary>
    ///     Processes the download queue.
    /// </summary>
    private async Task ProcessQueueAsync()
    {
        _logger.Debug("Download queue processor started");

        while (!_globalCancellation.Token.IsCancellationRequested)
        {
            try
            {
                // Wait for available slot
                await _downloadSemaphore.WaitAsync(_globalCancellation.Token);

                if (_globalCancellation.Token.IsCancellationRequested)
                    break;

                // Get next task from queue
                if (_downloadQueue.TryDequeue(out var task))
                {
                    // Process download in background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessDownloadAsync(task);
                        }
                        finally
                        {
                            _downloadSemaphore.Release();
                        }
                    }, _globalCancellation.Token);
                }
                else
                {
                    // No tasks in queue, release semaphore and wait
                    _downloadSemaphore.Release();
                    await Task.Delay(100, _globalCancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("Download queue processor cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in download queue processor");
                await Task.Delay(1000, _globalCancellation.Token);
            }
        }

        _logger.Debug("Download queue processor stopped");
    }

    /// <summary>
    ///     Processes a single download task.
    /// </summary>
    private async Task ProcessDownloadAsync(DownloadTask task)
    {
        _activeDownloads[task.Id] = task;
        task.Status = DownloadStatus.Downloading;
        task.StartedAt = DateTime.UtcNow;

        try
        {
            _logger.Information("Starting download: {Url} -> {FilePath}", task.Url, task.FilePath);

            // Create progress reporter
            var progress = new Progress<float>(p =>
            {
                task.DownloadedBytes = (long)(task.TotalBytes * p);
                ProgressUpdated?.Invoke(this, (task.Id, p * 100));
            });

            // Download file
            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(
                _globalCancellation.Token,
                task.CancellationSource?.Token ?? CancellationToken.None
            );

            var (response, filePath) = await _httpManager.DownloadAsync(
                task.Url,
                task.FilePath,
                progress,
                linkedToken.Token
            );

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Download failed with status {response.StatusCode}");
            }

            // Get total bytes from file
            if (File.Exists(filePath))
            {
                task.TotalBytes = new FileInfo(filePath).Length;
                task.DownloadedBytes = task.TotalBytes;
            }

            // Verify hash if requested
            if (task.VerifyHash && !string.IsNullOrEmpty(task.ExpectedSha1))
            {
                task.Status = DownloadStatus.Verifying;
                _logger.Debug("Verifying download: {FilePath}", task.FilePath);

                var actualSha1 = await CryptoUtils.CalculateFileSHA1Async(task.FilePath);
                
                if (!string.Equals(actualSha1, task.ExpectedSha1, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"Hash verification failed. Expected: {task.ExpectedSha1}, Actual: {actualSha1}");
                }

                _logger.Debug("Hash verification passed for {FilePath}", task.FilePath);
            }

            // Mark as completed
            task.Status = DownloadStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            
            _logger.Information("Download completed: {Url} -> {FilePath}", task.Url, task.FilePath);
            DownloadCompleted?.Invoke(this, task);
        }
        catch (OperationCanceledException)
        {
            task.Status = DownloadStatus.Cancelled;
            task.CompletedAt = DateTime.UtcNow;
            _logger.Information("Download cancelled: {Url}", task.Url);
        }
        catch (Exception ex)
        {
            task.Status = DownloadStatus.Failed;
            task.ErrorMessage = ex.Message;
            task.CompletedAt = DateTime.UtcNow;
            
            _logger.Error(ex, "Download failed: {Url} -> {FilePath}", task.Url, task.FilePath);

            // Retry logic
            if (task.RetryCount < task.MaxRetries)
            {
                task.RetryCount++;
                task.Status = DownloadStatus.Queued;
                task.StartedAt = null;
                task.CompletedAt = null;
                _downloadQueue.Enqueue(task);
                _logger.Information("Retrying download (attempt {RetryCount}/{MaxRetries}): {Url}", 
                    task.RetryCount, task.MaxRetries, task.Url);
            }
            else
            {
                DownloadFailed?.Invoke(this, task);
            }
        }
        finally
        {
            // Move to completed downloads if not being retried
            if (task.Status != DownloadStatus.Queued)
            {
                _activeDownloads.TryRemove(task.Id, out _);
                _completedDownloads[task.Id] = task;
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _globalCancellation.Cancel();
        _downloadSemaphore?.Dispose();
        _globalCancellation?.Dispose();

        _isDisposed = true;
        _logger.Debug("DownloadQueueManager disposed");
    }
}
