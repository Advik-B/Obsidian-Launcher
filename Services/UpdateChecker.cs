using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services;

/// <summary>
/// Checks GitHub Releases for newer launcher versions.
/// </summary>
public class UpdateChecker
{
    private const string ReleasesApiUrl = "https://api.github.com/repos/advik-b/obsidian-launcher/releases/latest";

    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public UpdateChecker(HttpManager httpManager)
    {
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _logger = LogHelper.GetLogger<UpdateChecker>();
    }

    /// <summary>
    /// Checks GitHub for a newer release.
    /// Returns update info if a newer version is available, null otherwise.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        _logger.Information("Checking for launcher updates...");

        try
        {
            // HttpManager already sets User-Agent header by default
            var response = await _httpManager.GetAsync(ReleasesApiUrl, cancellationToken: ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("Update check failed: {Status}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json, JsonOptions);

            if (release == null || string.IsNullOrEmpty(release.TagName))
            {
                _logger.Warning("Could not parse GitHub release response");
                return null;
            }

            var latestVersion = release.TagName.TrimStart('v', 'V');
            var currentVersion = LauncherConfig.VERSION.TrimStart('v', 'V');

            _logger.Information("Current: {Current}, Latest: {Latest}", currentVersion, latestVersion);

            if (IsNewerVersion(latestVersion, currentVersion))
            {
                return new UpdateInfo
                {
                    LatestVersion = latestVersion,
                    CurrentVersion = currentVersion,
                    ReleaseUrl = release.HtmlUrl ?? ReleasesApiUrl,
                    ReleaseNotes = release.Body ?? string.Empty,
                    PublishedAt = release.PublishedAt
                };
            }

            _logger.Information("Launcher is up to date");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Exception during update check");
            return null;
        }
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        // Normalize: strip "-snapshot", "-beta", etc. for comparison
        static string Normalize(string v)
        {
            var idx = v.IndexOfAny(new[] { '-', '+' });
            return idx >= 0 ? v.Substring(0, idx) : v;
        }

        if (Version.TryParse(Normalize(latest), out var latestVer) &&
            Version.TryParse(Normalize(current), out var currentVer))
        {
            return latestVer > currentVer;
        }

        // Fallback: string comparison
        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
    }
}

public class UpdateInfo
{
    public string LatestVersion { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string ReleaseUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string? PublishedAt { get; set; }
}

#region GitHub API Models

public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("published_at")]
    public string? PublishedAt { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }
}

#endregion
