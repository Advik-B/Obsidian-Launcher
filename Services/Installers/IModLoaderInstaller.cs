using System.Threading;
using System.Threading.Tasks;

namespace ObsidianLauncher.Services.Installers;

/// <summary>
/// Defines a contract for installing a mod loader into the launcher's version list.
/// </summary>
public interface IModLoaderInstaller
{
    /// <summary>
    /// The unique, case-insensitive name of the mod loader (e.g., "Forge", "Fabric").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Installs a specific version of the mod loader for a given Minecraft version.
    /// </summary>
    /// <param name="minecraftVersion">The target Minecraft version (e.g., "1.20.4").</param>
    /// <param name="loaderVersion">The target mod loader version (e.g., "49.0.23").</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The new version ID string if installation is successful; otherwise, null.</returns>
    Task<string> InstallAsync(string minecraftVersion, string loaderVersion, CancellationToken cancellationToken = default);
}