// Services/HttpCacheManager.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services;

/// <summary>
///     Manages HTTP response caching with ETag and Last-Modified support.
/// </summary>
public class HttpCacheManager
{
    private readonly ILogger _logger;
    private readonly string _cacheDirectory;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache;
    private readonly TimeSpan _defaultCacheDuration;

    public HttpCacheManager(string cacheDirectory, TimeSpan? defaultCacheDuration = null)
    {
        _logger = LogHelper.GetLogger<HttpCacheManager>();
        _cacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
        _cache = new ConcurrentDictionary<string, CacheEntry>();
        _defaultCacheDuration = defaultCacheDuration ?? TimeSpan.FromHours(1);

        Directory.CreateDirectory(_cacheDirectory);
        LoadCacheIndex();

        _logger.Information("HttpCacheManager initialized with cache directory: {CacheDirectory}", _cacheDirectory);
    }

    /// <summary>
    ///     Gets a cache key for a URL.
    /// </summary>
    private string GetCacheKey(string url)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }

    /// <summary>
    ///     Checks if a response is cached and valid.
    /// </summary>
    public bool IsCached(string url, out CacheEntry? entry)
    {
        var key = GetCacheKey(url);

        if (_cache.TryGetValue(key, out entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                return true;
            }

            // Cache expired
            _cache.TryRemove(key, out _);
        }

        entry = null;
        return false;
    }

    /// <summary>
    ///     Gets cached content for a URL.
    /// </summary>
    public async Task<string?> GetCachedContentAsync(string url)
    {
        if (IsCached(url, out var entry) && entry != null)
        {
            var contentPath = Path.Combine(_cacheDirectory, entry.ContentFile);

            if (File.Exists(contentPath))
            {
                _logger.Debug("Cache hit for {Url}", url);
                return await File.ReadAllTextAsync(contentPath);
            }

            // File missing, remove from cache
            _cache.TryRemove(GetCacheKey(url), out _);
        }

        _logger.Debug("Cache miss for {Url}", url);
        return null;
    }

    /// <summary>
    ///     Stores content in cache.
    /// </summary>
    public async Task StoreInCacheAsync(
        string url,
        string content,
        string? etag = null,
        DateTime? lastModified = null,
        TimeSpan? cacheDuration = null)
    {
        var key = GetCacheKey(url);
        var contentFile = $"{key}.txt";
        var contentPath = Path.Combine(_cacheDirectory, contentFile);

        // Write content to file
        await File.WriteAllTextAsync(contentPath, content);

        // Create cache entry
        var entry = new CacheEntry
        {
            Url = url,
            ContentFile = contentFile,
            ETag = etag,
            LastModified = lastModified,
            CachedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow + (cacheDuration ?? _defaultCacheDuration)
        };

        _cache[key] = entry;
        SaveCacheIndex();

        _logger.Debug("Stored in cache: {Url} (expires: {ExpiresAt})", url, entry.ExpiresAt);
    }

    /// <summary>
    ///     Gets conditional request headers for cached content.
    /// </summary>
    public void AddConditionalHeaders(HttpRequestMessage request, string url)
    {
        if (IsCached(url, out var entry) && entry != null)
        {
            if (!string.IsNullOrEmpty(entry.ETag))
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", entry.ETag);
                _logger.Debug("Added If-None-Match header: {ETag}", entry.ETag);
            }

            if (entry.LastModified.HasValue)
            {
                request.Headers.TryAddWithoutValidation(
                    "If-Modified-Since",
                    entry.LastModified.Value.ToString("R"));
                _logger.Debug("Added If-Modified-Since header: {LastModified}", entry.LastModified.Value);
            }
        }
    }

    /// <summary>
    ///     Invalidates cache for a URL.
    /// </summary>
    public void Invalidate(string url)
    {
        var key = GetCacheKey(url);

        if (_cache.TryRemove(key, out var entry))
        {
            var contentPath = Path.Combine(_cacheDirectory, entry.ContentFile);

            if (File.Exists(contentPath))
            {
                File.Delete(contentPath);
            }

            _logger.Debug("Invalidated cache for {Url}", url);
            SaveCacheIndex();
        }
    }

    /// <summary>
    ///     Clears all cached content.
    /// </summary>
    public void ClearAll()
    {
        _cache.Clear();

        if (Directory.Exists(_cacheDirectory))
        {
            foreach (var file in Directory.GetFiles(_cacheDirectory))
            {
                File.Delete(file);
            }
        }

        SaveCacheIndex();
        _logger.Information("Cleared all HTTP cache");
    }

    /// <summary>
    ///     Removes expired cache entries.
    /// </summary>
    public void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = new List<string>();

        foreach (var kvp in _cache)
        {
            if (kvp.Value.ExpiresAt < now)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            if (_cache.TryRemove(key, out var entry))
            {
                var contentPath = Path.Combine(_cacheDirectory, entry.ContentFile);

                if (File.Exists(contentPath))
                {
                    File.Delete(contentPath);
                }
            }
        }

        if (expiredKeys.Count > 0)
        {
            SaveCacheIndex();
            _logger.Information("Cleaned up {Count} expired cache entries", expiredKeys.Count);
        }
    }

    /// <summary>
    ///     Loads the cache index from disk.
    /// </summary>
    private void LoadCacheIndex()
    {
        var indexPath = Path.Combine(_cacheDirectory, "cache_index.json");

        if (!File.Exists(indexPath))
            return;

        try
        {
            var json = File.ReadAllText(indexPath);
            var entries = JsonSerializer.Deserialize<CacheEntry[]>(json);

            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    var key = GetCacheKey(entry.Url);
                    _cache[key] = entry;
                }

                _logger.Debug("Loaded {Count} cache entries from index", entries.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load cache index");
        }
    }

    /// <summary>
    ///     Saves the cache index to disk.
    /// </summary>
    private void SaveCacheIndex()
    {
        var indexPath = Path.Combine(_cacheDirectory, "cache_index.json");

        try
        {
            var entries = _cache.Values.ToArray();
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(indexPath, json);
            _logger.Debug("Saved cache index with {Count} entries", entries.Length);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save cache index");
        }
    }
}

/// <summary>
///     Represents a cached HTTP response.
/// </summary>
public class CacheEntry
{
    public string Url { get; set; } = string.Empty;
    public string ContentFile { get; set; } = string.Empty;
    public string? ETag { get; set; }
    public DateTime? LastModified { get; set; }
    public DateTime CachedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
