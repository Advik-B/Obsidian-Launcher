using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services.ModLoaders;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services;

public class ModLoaderService
{
    private readonly HttpManager _httpManager;
    private readonly LauncherConfig? _config;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ModLoaderService(HttpManager httpManager, LauncherConfig? config = null)
    {
        _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
        _config = config;
        _logger = LogHelper.GetLogger<ModLoaderService>();
    }

    /// <summary>
    /// Fetches the version-specific launch profile JSON for a given mod loader component.
    /// Supports Fabric, Quilt, Forge, and NeoForge.
    /// </summary>
    /// <param name="mcVersionHint">The Minecraft game version, used to construct Fabric/Quilt profile URLs when not encoded in the component version string.</param>
    public async Task<MinecraftVersion?> GetModLoaderVersionAsync(Component component, CancellationToken cancellationToken = default, string? mcVersionHint = null)
    {
        _logger.Information("Fetching version details for mod loader: {Uid} {Version}", component.Uid, component.Version);

        return component.Uid switch
        {
            "net.fabricmc.fabric-loader" => await GetFabricProfileAsync(component, cancellationToken, mcVersionHint),
            "org.quiltmc.quilt-loader" => await GetQuiltProfileAsync(component, cancellationToken, mcVersionHint),
            "net.minecraftforge" => await GetForgeProfileAsync(component, cancellationToken),
            "net.neoforged.neoforge" => await GetNeoForgeProfileAsync(component, cancellationToken),
            _ => await GetGenericProfileAsync(component, cancellationToken)
        };
    }

    private async Task<MinecraftVersion?> GetFabricProfileAsync(Component component, CancellationToken ct, string? mcVersionHint = null)
    {
        var loaderVersion = component.Version;

        // Combined "gameVer/loaderVer" encoding takes priority
        if (loaderVersion.Contains('/'))
        {
            var parts = loaderVersion.Split('/');
            return await new FabricInstaller(_httpManager).GetProfileAsync(parts[0], parts[1], ct);
        }

        // Use the MC version hint when available (passed from BuildLaunchProfileAsync)
        if (!string.IsNullOrEmpty(mcVersionHint))
            return await new FabricInstaller(_httpManager).GetProfileAsync(mcVersionHint, loaderVersion, ct);

        _logger.Warning("Fabric component version {Version} has no game version context. Provide mcVersionHint or encode as 'gameVer/loaderVer'.", loaderVersion);
        return null;
    }

    private async Task<MinecraftVersion?> GetQuiltProfileAsync(Component component, CancellationToken ct, string? mcVersionHint = null)
    {
        var installer = new QuiltInstaller(_httpManager);
        var version = component.Version;

        if (version.Contains('/'))
        {
            var parts = version.Split('/');
            return await installer.GetProfileAsync(parts[0], parts[1], ct);
        }

        if (!string.IsNullOrEmpty(mcVersionHint))
            return await installer.GetProfileAsync(mcVersionHint, version, ct);

        _logger.Warning("Quilt component version {Version} has no game version context.", version);
        return null;
    }

    private async Task<MinecraftVersion?> GetForgeProfileAsync(Component component, CancellationToken ct)
    {
        if (_config == null)
        {
            _logger.Error("LauncherConfig not available for Forge installer");
            return null;
        }

        var installer = new ForgeInstaller(_httpManager, _config);
        var version = component.Version;

        // version may be "1.20.1-47.2.0" or just "47.2.0"
        if (version.Contains('-'))
        {
            var dashIdx = version.IndexOf('-');
            var mcVersion = version.Substring(0, dashIdx);
            return await installer.GetProfileAsync(mcVersion, version, ct);
        }

        _logger.Warning("Forge component version {Version} does not contain MC version prefix. Cannot install.", version);
        return null;
    }

    private async Task<MinecraftVersion?> GetNeoForgeProfileAsync(Component component, CancellationToken ct)
    {
        if (_config == null)
        {
            _logger.Error("LauncherConfig not available for NeoForge installer");
            return null;
        }

        var installer = new NeoForgeInstaller(_httpManager, _config);
        return await installer.GetProfileAsync(component.Version, ct);
    }

    private async Task<MinecraftVersion?> GetGenericProfileAsync(Component component, CancellationToken ct)
    {
        _logger.Warning("No specific handler for mod loader UID '{Uid}'. No profile will be fetched.", component.Uid);
        return null;
    }

    private async Task<MinecraftVersion?> FetchAndParseAsync(string url, CancellationToken ct)
    {
        try
        {
            var response = await _httpManager.GetAsync(url, cancellationToken: ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Error("Failed to fetch mod loader manifest from {Url}: {Status}", url, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<MinecraftVersion>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception fetching manifest from {Url}", url);
            return null;
        }
    }
}
