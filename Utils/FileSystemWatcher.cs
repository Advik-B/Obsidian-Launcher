// Utils/FileSystemWatcher.cs

using System;
using System.Collections.Generic;
using System.IO;
using Serilog;

namespace ObsidianLauncher.Utils;

/// <summary>
///     Utility for watching directory changes recursively.
///     Provides events for file creation, modification, deletion, and renaming.
/// </summary>
public class RecursiveFileWatcher : IDisposable
{
    private static readonly ILogger _logger = Log.ForContext(typeof(RecursiveFileWatcher));
    
    private readonly FileSystemWatcher _watcher;
    private readonly Dictionary<string, FileSystemWatcher> _subWatchers;
    private bool _disposed;

    /// <summary>
    ///     Occurs when a file is created.
    /// </summary>
    public event EventHandler<FileSystemEventArgs>? FileCreated;

    /// <summary>
    ///     Occurs when a file is changed.
    /// </summary>
    public event EventHandler<FileSystemEventArgs>? FileChanged;

    /// <summary>
    ///     Occurs when a file is deleted.
    /// </summary>
    public event EventHandler<FileSystemEventArgs>? FileDeleted;

    /// <summary>
    ///     Occurs when a file is renamed.
    /// </summary>
    public event EventHandler<RenamedEventArgs>? FileRenamed;

    /// <summary>
    ///     Creates a new recursive file system watcher.
    /// </summary>
    /// <param name="path">The path to watch.</param>
    /// <param name="filter">File filter pattern (e.g., "*.txt", "*.*").</param>
    /// <param name="includeSubdirectories">Whether to watch subdirectories recursively.</param>
    public RecursiveFileWatcher(string path, string filter = "*.*", bool includeSubdirectories = true)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }

        _subWatchers = new Dictionary<string, FileSystemWatcher>();
        
        _watcher = new FileSystemWatcher(path)
        {
            Filter = filter,
            IncludeSubdirectories = includeSubdirectories,
            NotifyFilter = NotifyFilters.FileName | 
                          NotifyFilters.DirectoryName | 
                          NotifyFilters.LastWrite | 
                          NotifyFilters.Size | 
                          NotifyFilters.CreationTime
        };

        _watcher.Created += OnCreated;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnDeleted;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnError;

        _logger.Information("Created file watcher for: {Path} (filter: {Filter}, recursive: {Recursive})", 
            path, filter, includeSubdirectories);
    }

    /// <summary>
    ///     Starts watching for file system changes.
    /// </summary>
    public void Start()
    {
        _watcher.EnableRaisingEvents = true;
        _logger.Information("Started watching: {Path}", _watcher.Path);
    }

    /// <summary>
    ///     Stops watching for file system changes.
    /// </summary>
    public void Stop()
    {
        _watcher.EnableRaisingEvents = false;
        _logger.Information("Stopped watching: {Path}", _watcher.Path);
    }

    /// <summary>
    ///     Gets whether the watcher is currently enabled.
    /// </summary>
    public bool IsWatching => _watcher.EnableRaisingEvents;

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        _logger.Debug("File created: {Path}", e.FullPath);
        FileCreated?.Invoke(this, e);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        _logger.Debug("File changed: {Path}", e.FullPath);
        FileChanged?.Invoke(this, e);
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        _logger.Debug("File deleted: {Path}", e.FullPath);
        FileDeleted?.Invoke(this, e);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        _logger.Debug("File renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
        FileRenamed?.Invoke(this, e);
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        _logger.Error(ex, "File watcher error");
    }

    /// <summary>
    ///     Disposes the file watcher and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();

        foreach (var subWatcher in _subWatchers.Values)
        {
            subWatcher.Dispose();
        }
        _subWatchers.Clear();

        _disposed = true;
        _logger.Debug("Disposed file watcher for: {Path}", _watcher.Path);
    }
}
