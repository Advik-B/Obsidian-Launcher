// Utils/PathUtils.cs

using System;
using System.IO;
using System.Runtime.InteropServices;
using Serilog;

namespace ObsidianLauncher.Utils;

/// <summary>
///     Utility class for platform-specific path resolution and manipulation.
/// </summary>
public static class PathUtils
{
    private static readonly ILogger _logger = Log.ForContext(typeof(PathUtils));

    /// <summary>
    ///     Gets the user's home directory path.
    /// </summary>
    /// <returns>The home directory path.</returns>
    public static string GetHomeDirectory()
    {
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrEmpty(homePath))
        {
            // Fallback for older systems or special environments
            homePath = Environment.GetEnvironmentVariable("HOME")
                      ?? Environment.GetEnvironmentVariable("USERPROFILE")
                      ?? "/tmp";
        }

        _logger.Verbose("Home directory: {Path}", homePath);
        return homePath;
    }

    /// <summary>
    ///     Gets the application data directory for the current user.
    /// </summary>
    /// <returns>The application data directory path.</returns>
    public static string GetAppDataDirectory()
    {
        string appDataPath;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            appDataPath = Path.Combine(GetHomeDirectory(), "Library", "Application Support");
        }
        else // Linux and others
        {
            appDataPath = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                         ?? Path.Combine(GetHomeDirectory(), ".config");
        }

        _logger.Verbose("App data directory: {Path}", appDataPath);
        return appDataPath;
    }

    /// <summary>
    ///     Gets the local application data directory.
    /// </summary>
    /// <returns>The local app data directory path.</returns>
    public static string GetLocalAppDataDirectory()
    {
        string localDataPath;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            localDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            localDataPath = Path.Combine(GetHomeDirectory(), "Library", "Application Support");
        }
        else // Linux
        {
            localDataPath = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
                           ?? Path.Combine(GetHomeDirectory(), ".local", "share");
        }

        _logger.Verbose("Local app data directory: {Path}", localDataPath);
        return localDataPath;
    }

    /// <summary>
    ///     Gets the cache directory.
    /// </summary>
    /// <returns>The cache directory path.</returns>
    public static string GetCacheDirectory()
    {
        string cachePath;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cache");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            cachePath = Path.Combine(GetHomeDirectory(), "Library", "Caches");
        }
        else // Linux
        {
            cachePath = Environment.GetEnvironmentVariable("XDG_CACHE_HOME")
                       ?? Path.Combine(GetHomeDirectory(), ".cache");
        }

        _logger.Verbose("Cache directory: {Path}", cachePath);
        return cachePath;
    }

    /// <summary>
    ///     Gets the temporary directory.
    /// </summary>
    /// <returns>The temporary directory path.</returns>
    public static string GetTempDirectory()
    {
        var tempPath = Path.GetTempPath();
        _logger.Verbose("Temp directory: {Path}", tempPath);
        return tempPath;
    }

    /// <summary>
    ///     Normalizes a path to use the correct directory separator for the current platform.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path.</returns>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Replace all separators with the platform-specific one
        var normalized = path.Replace('\\', Path.DirectorySeparatorChar)
                            .Replace('/', Path.DirectorySeparatorChar);

        _logger.Verbose("Normalized path: {Original} -> {Normalized}", path, normalized);
        return normalized;
    }

    /// <summary>
    ///     Combines path parts using the platform-specific separator.
    /// </summary>
    /// <param name="paths">Path parts to combine.</param>
    /// <returns>The combined path.</returns>
    public static string CombinePaths(params string[] paths)
    {
        return Path.Combine(paths);
    }

    /// <summary>
    ///     Makes a path absolute if it's relative.
    /// </summary>
    /// <param name="path">The path to make absolute.</param>
    /// <param name="basePath">The base path for relative paths (defaults to current directory).</param>
    /// <returns>The absolute path.</returns>
    public static string MakeAbsolute(string path, string? basePath = null)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        if (Path.IsPathRooted(path))
            return path;

        basePath ??= Directory.GetCurrentDirectory();
        var absolutePath = Path.GetFullPath(Path.Combine(basePath, path));

        _logger.Verbose("Made path absolute: {Relative} -> {Absolute}", path, absolutePath);
        return absolutePath;
    }

    /// <summary>
    ///     Makes a path relative to a base path.
    /// </summary>
    /// <param name="path">The path to make relative.</param>
    /// <param name="basePath">The base path.</param>
    /// <returns>The relative path.</returns>
    public static string MakeRelative(string path, string basePath)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(basePath))
            return path;

        var pathUri = new Uri(Path.GetFullPath(path));
        var baseUri = new Uri(Path.GetFullPath(basePath) + Path.DirectorySeparatorChar);

        var relativeUri = baseUri.MakeRelativeUri(pathUri);
        var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

        // Convert URI separators back to platform separators
        relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);

        _logger.Verbose("Made path relative: {Absolute} -> {Relative} (base: {Base})",
            path, relativePath, basePath);

        return relativePath;
    }

    /// <summary>
    ///     Ensures a directory exists, creating it if necessary.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>True if the directory exists or was created, false on error.</returns>
    public static bool EnsureDirectoryExists(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                _logger.Debug("Created directory: {Path}", path);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create directory: {Path}", path);
            return false;
        }
    }

    /// <summary>
    ///     Gets a safe filename by removing invalid characters.
    /// </summary>
    /// <param name="filename">The filename to sanitize.</param>
    /// <param name="replacement">The replacement character for invalid characters.</param>
    /// <returns>A safe filename.</returns>
    public static string GetSafeFilename(string filename, char replacement = '_')
    {
        if (string.IsNullOrEmpty(filename))
            return filename;

        var invalidChars = Path.GetInvalidFileNameChars();
        var safeFilename = filename;

        foreach (var c in invalidChars)
        {
            safeFilename = safeFilename.Replace(c, replacement);
        }

        return safeFilename;
    }

    /// <summary>
    ///     Gets a safe path by removing invalid characters.
    /// </summary>
    /// <param name="path">The path to sanitize.</param>
    /// <param name="replacement">The replacement character for invalid characters.</param>
    /// <returns>A safe path.</returns>
    public static string GetSafePath(string path, char replacement = '_')
    {
        if (string.IsNullOrEmpty(path))
            return path;

        var invalidChars = Path.GetInvalidPathChars();
        var safePath = path;

        foreach (var c in invalidChars)
        {
            safePath = safePath.Replace(c, replacement);
        }

        return safePath;
    }
}
