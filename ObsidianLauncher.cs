using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Enums;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using ObsidianLauncher.Services.Installers;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher;

public class ObsidianLauncher
{
    private static readonly CancellationTokenSource _cts = new();

    private static async Task Main(string[] args)
    {
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Log.Warning("Cancellation requested via Ctrl+C.");
            _cts.Cancel();
            eventArgs.Cancel = true;
        };

        LauncherConfig launcherConfig = null;
        try
        {
            launcherConfig = new LauncherConfig();
            LoggerSetup.Initialize(launcherConfig);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Critical startup error: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            Environment.ExitCode = 1;
            return;
        }

        using var httpManager = new HttpManager();

        // --- List Forge Versions Command ---
        if (args.Length > 0 && args[0].Equals("--list-forge-versions", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2)
            {
                Log.Error("Usage: --list-forge-versions <minecraft_version>");
                Log.Error("Example: --list-forge-versions 1.16.1");
                await Log.CloseAndFlushAsync();
                return;
            }
            
            var mcVersion = args[1];
            var metadataService = new ForgeMetadataService(httpManager);
            var forgeVersions = await metadataService.GetForgeVersionsAsync(mcVersion, _cts.Token);

            if (forgeVersions != null && forgeVersions.Any())
            {
                Console.WriteLine($"Available Forge Versions for Minecraft {mcVersion}:");
                Console.WriteLine(new string('-', 60));
                Console.WriteLine($"{"Version",-20} | {"Build",-12} | {"Release Date",-15}");
                Console.WriteLine(new string('-', 60));
                
                foreach (var version in forgeVersions)
                {
                    Console.WriteLine($"{version.Version,-20} | {version.Build,-12} | {version.Modified:yyyy-MM-dd}");
                }
                
                Console.WriteLine(new string('-', 60));
                Log.Information("To install one of these, use the '--install-modloader Forge {MCVersion} <version>' command.", mcVersion);
            }
            else
            {
                Log.Warning("Could not retrieve Forge versions for Minecraft {MCVersion}. The version may not be supported or the API may be down.", mcVersion);
            }

            await Log.CloseAndFlushAsync();
            return;
        }
        
        // --- Mod Loader Installation Command ---
        if (args.Length > 0 && args[0].Equals("--install-modloader", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 4)
            {
                Log.Error("Usage: --install-modloader <loader_name> <minecraft_version> <loader_version>");
                Log.Error("Example: --install-modloader Forge 1.20.4 49.0.23");
                await Log.CloseAndFlushAsync();
                return;
            }

            string loaderName = args[1];
            string mcVersion = args[2];
            string loaderVersion = args[3];
            
            var installers = new List<IModLoaderInstaller>
            {
                new ForgeInstaller(launcherConfig, httpManager)
                // When you add Fabric, you'll just add it here:
                // new FabricInstaller(launcherConfig, httpManager)
            };
            
            var installer = installers.FirstOrDefault(i => i.Name.Equals(loaderName, StringComparison.OrdinalIgnoreCase));

            if (installer == null)
            {
                Log.Error("Could not find an installer for '{LoaderName}'. Supported loaders: {Supported}",
                    loaderName, string.Join(", ", installers.Select(i => i.Name)));
                await Log.CloseAndFlushAsync();
                return;
            }

            string newVersionId = await installer.InstallAsync(mcVersion, loaderVersion, _cts.Token);

            if (!string.IsNullOrEmpty(newVersionId))
            {
                Log.Information("Installation successful. New Version ID: '{VersionId}'", newVersionId);
                Log.Information("You can now launch this version, for example: .\\ObsidianLauncher.exe \"{VersionId}\"", newVersionId);
            }
            else
            {
                Log.Error("Installation failed. Check logs for details.");
            }
            
            await Log.CloseAndFlushAsync();
            return;
        }
        
        // --- Default Game Launch Logic ---
        Log.Information("==================================================");
        Log.Information("  Obsidian Launcher {Version}", $"v{LauncherConfig.VERSION}");
        Log.Information("==================================================");
        Log.Information("Global data directory: {BaseDataPath}", launcherConfig.BaseDataPath);
        Log.Information("Instances root directory: {InstancesRootDir}", launcherConfig.InstancesRootDir);
        Log.Information("Launcher log directory: {LogsDir}", launcherConfig.LogsDir);

        var javaManager = new JavaManager(launcherConfig, httpManager);
        var assetManager = new AssetManager(launcherConfig, httpManager);
        var libraryManager = new LibraryManager(launcherConfig, httpManager);
        var instanceManager = new InstanceManager(launcherConfig, assetManager, libraryManager);
        var argumentBuilder = new ArgumentBuilder(launcherConfig);
        var gameLauncher = new GameLauncher(launcherConfig);

        try
        {
            Log.Information("Fetching Minecraft version manifest from Mojang...");
            var manifestResponseMsg = await httpManager.GetAsync(
                "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json",
                cancellationToken: _cts.Token);

            if (_cts.IsCancellationRequested)
            {
                Log.Warning("Manifest fetch cancelled.");
                return;
            }

            if (!manifestResponseMsg.IsSuccessStatusCode)
            {
                var errorContent = await manifestResponseMsg.Content.ReadAsStringAsync(_cts.Token);
                Log.Fatal("Failed to fetch version manifest. Status: {StatusCode}, URL: {Url}, Error: {ErrorContent}",
                    manifestResponseMsg.StatusCode, manifestResponseMsg.RequestMessage?.RequestUri, errorContent);
                return;
            }

            var manifestJsonString = await manifestResponseMsg.Content.ReadAsStringAsync(_cts.Token);
            Log.Information("Successfully fetched version manifest (status {StatusCode}). Size: {Length} bytes",
                manifestResponseMsg.StatusCode, manifestJsonString.Length);

            var versionManifestAll = JsonSerializer.Deserialize<VersionManifest>(manifestJsonString,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (versionManifestAll?.Versions == null)
            {
                Log.Fatal("Failed to parse version manifest or no versions found.");
                return;
            }

            Log.Verbose("Version manifest JSON parsed successfully. Found {Count} versions.",
                versionManifestAll.Versions.Count);

            var versionIdToLaunch = "1.20.4";
            string cliInstanceNameFromArg = null;
            string cliPlayerNameFromArg = null;

            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                versionIdToLaunch = args[0];
                Log.Information("Target version from command line: {VersionId}", versionIdToLaunch);
                if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
                {
                    cliInstanceNameFromArg = args[1];
                    Log.Information("Target instance name from command line: {InstanceName}", cliInstanceNameFromArg);
                }

                if (args.Length > 2 && !string.IsNullOrWhiteSpace(args[2]))
                {
                    cliPlayerNameFromArg = args[2];
                    Log.Information("Player name for this session from command line: {PlayerName}",
                        cliPlayerNameFromArg);
                }
            }

            Log.Information("Target Minecraft version for setup: {VersionId}", versionIdToLaunch);

            var sessionPlayerName = cliPlayerNameFromArg ?? $"Player{Random.Shared.Next(100, 999)}";
            Log.Information("Using player name for this session: {PlayerName}", sessionPlayerName);
            argumentBuilder.SetOfflinePlayerName(sessionPlayerName);


            var selectedVersionMeta = versionManifestAll.Versions.FirstOrDefault(v => v.Id == versionIdToLaunch);
            string versionDetailsJsonString; // <<<<< FIX: Declared variable in the outer scope

            if (selectedVersionMeta == null)
            {
                Log.Error("Target version '{VersionId}' not found in official Mojang version manifest. If this is a modded version, this is expected. Attempting to load from local files.", versionIdToLaunch);
                var localVersionJsonPath = Path.Combine(launcherConfig.VersionsDir, versionIdToLaunch, $"{versionIdToLaunch}.json");
                if (!File.Exists(localVersionJsonPath))
                {
                    Log.Fatal("Target version '{VersionId}' not found in Mojang manifest AND not found locally at {Path}. Cannot proceed.", versionIdToLaunch, localVersionJsonPath);
                    return;
                }
                
                Log.Information("Loading version details from local file: {Path}", localVersionJsonPath);
                var localJson = await File.ReadAllTextAsync(localVersionJsonPath, _cts.Token);
                // <<<<< FIX: Initialized the required 'Type' member
                selectedVersionMeta = new VersionMetadata { Url = null, Id = versionIdToLaunch, Type = "local" }; 
                versionDetailsJsonString = localJson; // <<<<< FIX: Assigned to the outer-scoped variable
            }
            else
            {
                Log.Information("Found URL for version '{VersionId}': {Url}", versionIdToLaunch, selectedVersionMeta.Url);
                Log.Information("Fetching details for version '{VersionId}'...", versionIdToLaunch);
                var versionDetailsResponseMsg = await httpManager.GetAsync(selectedVersionMeta.Url, cancellationToken: _cts.Token);
                
                if (_cts.IsCancellationRequested) { Log.Warning("Version details fetch cancelled."); return; }

                if (!versionDetailsResponseMsg.IsSuccessStatusCode)
                {
                    var errorContent = await versionDetailsResponseMsg.Content.ReadAsStringAsync(_cts.Token);
                    Log.Error("Failed to fetch version details for '{VersionId}'. Status: {StatusCode}, URL: {Url}, Error: {ErrorContent}", versionIdToLaunch, versionDetailsResponseMsg.StatusCode, versionDetailsResponseMsg.RequestMessage?.RequestUri, errorContent);
                    return;
                }
                // <<<<< FIX: Assigned to the outer-scoped variable, removed 'var'
                versionDetailsJsonString = await versionDetailsResponseMsg.Content.ReadAsStringAsync(_cts.Token);
            }

            // <<<<< FIX: Now this code works regardless of which path was taken above
            Log.Information("Successfully fetched/loaded version details for '{VersionId}'. Size: {Length} bytes",
                versionIdToLaunch, versionDetailsJsonString.Length);

            var minecraftVersion = JsonSerializer.Deserialize<MinecraftVersion>(versionDetailsJsonString,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (minecraftVersion == null)
            {
                Log.Fatal("Failed to parse details for version '{VersionId}'.", versionIdToLaunch);
                return;
            }

            Log.Information("Successfully parsed Minecraft version object: {Id} (Type: {Type})", minecraftVersion.Id,
                minecraftVersion.Type);

            var instanceNameToUse = cliInstanceNameFromArg ?? $"Instance_{minecraftVersion.Id}";

            var assetProgress = new Progress<AssetDownloadProgress>(report =>
            {
                if (report.ProcessedFiles % Math.Max(1, report.TotalFiles / 20) == 0 ||
                    report.ProcessedFiles == report.TotalFiles)
                    Log.Information("[Assets] Progress: {Processed}/{Total} files ({OverallPercent:F1}%) - Current: {CurrentFile}", report.ProcessedFiles, report.TotalFiles, report.TotalFiles > 0 ? (double)report.ProcessedFiles / report.TotalFiles * 100 : 0, report.CurrentFile ?? "...");
            });
            var libraryProgress = new Progress<LibraryProcessingProgress>(report =>
            {
                if (report.Status.Contains("failed", StringComparison.OrdinalIgnoreCase) || report.Status.Contains("Skipped") || report.ProcessedLibraries % Math.Max(1, report.TotalLibraries / 10) == 0 || report.ProcessedLibraries == report.TotalLibraries)
                    Log.Information("[Libs] {Processed}/{Total} - Status: {Status} - Lib: {LibraryName}", report.ProcessedLibraries, report.TotalLibraries, report.Status, report.CurrentLibraryName);
                else
                    Log.Verbose("[Libs] {Processed}/{Total} - Status: {Status} - Lib: {LibraryName}", report.ProcessedLibraries, report.TotalLibraries, report.Status, report.CurrentLibraryName);
            });

            var (currentInstance, clientJarPath, libraryClasspathEntries) =
                await instanceManager.GetOrCreateInstanceAsync(instanceNameToUse, minecraftVersion.Id, minecraftVersion, assetProgress, libraryProgress, _cts.Token);

            if (currentInstance == null || libraryClasspathEntries == null)
            {
                Log.Fatal("Failed to get, create, or sync instance '{InstanceName}'. Cannot proceed.", instanceNameToUse);
                return;
            }

            Log.Information("Instance ready: '{InstanceName}' (ID: {InstanceId}) at {InstancePath}", currentInstance.Name, currentInstance.Id, currentInstance.InstancePath);

            Log.Information("--- Ensuring Java Runtime for Minecraft {VersionId} ---", minecraftVersion.Id);
            JavaRuntimeInfo javaRuntime = await javaManager.EnsureJavaForMinecraftVersionAsync(minecraftVersion, _cts.Token);
            
            if (_cts.IsCancellationRequested) { Log.Warning("Java setup cancelled."); return; }
            if (javaRuntime == null) { Log.Error("Failed to obtain a suitable Java runtime for Minecraft version '{VersionId}'. Cannot proceed.", minecraftVersion.Id); return; }

            Log.Information("Java Runtime Ensured: {JavaExecutablePath}", javaRuntime.JavaExecutablePath);

            Log.Information("--- Constructing Classpath ---");
            var classpathString = argumentBuilder.BuildClasspath(clientJarPath, libraryClasspathEntries);

            Log.Information("--- Constructing JVM Arguments ---");
            var jvmArgs = argumentBuilder.BuildJvmArguments(minecraftVersion, classpathString, Path.GetFullPath(currentInstance.NativesPath), javaRuntime, currentInstance.InstancePath);
            if (currentInstance.CustomJvmArguments != null && currentInstance.CustomJvmArguments.Any())
            {
                Log.Information("Applying {Count} custom JVM arguments from instance '{InstanceName}'...", currentInstance.CustomJvmArguments.Count, currentInstance.Name);
                jvmArgs.AddRange(currentInstance.CustomJvmArguments);
            }

            Log.Information("--- Constructing Game Arguments ---");
            var gameArgs = argumentBuilder.BuildGameArguments(minecraftVersion, currentInstance.InstancePath);

            Log.Information("--- Launching Minecraft {VersionId} for instance '{InstanceName}' (Player: {PlayerName}) ---", minecraftVersion.Id, currentInstance.Name, sessionPlayerName);
            var gameWorkingDirectory = Path.GetFullPath(currentInstance.GameDataPath);
            Log.Information("Game working directory (instance path) set to: {GameDir}", gameWorkingDirectory);

            var sessionStartTime = DateTime.UtcNow;
            var exitCode = await gameLauncher.LaunchAsync(javaRuntime.JavaExecutablePath, jvmArgs, minecraftVersion.MainClass, gameArgs, gameWorkingDirectory, _cts.Token);
            var sessionDuration = DateTime.UtcNow - sessionStartTime;
            await instanceManager.UpdateLastPlayedAsync(currentInstance, sessionDuration);

            if (_cts.IsCancellationRequested)
            {
                Log.Warning("Minecraft launch was explicitly cancelled by the user during execution.");
            }
            else
            {
                Log.Information("Minecraft process finished with exit code: {ExitCode}", exitCode);
                Log.Information("Instance '{InstanceName}' - Session Playtime: {SessionPlaytimeFormat}, Total Playtime: {TotalPlaytimeFormat}", currentInstance.Name, sessionDuration.ToString(@"hh\:mm\:ss"), currentInstance.TotalPlaytime.ToString(@"d\.hh\:mm\:ss"));
            }
        }
        catch (OperationCanceledException)
        {
            Log.Warning("A critical operation was cancelled. Launcher will now exit.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "An unhandled exception occurred in the main application flow. Launcher will now exit.");
            Environment.ExitCode = 1;
        }
        finally
        {
            Log.Information("Shutting down logger...");
            await Log.CloseAndFlushAsync();
            if (Environment.ExitCode != 0 || _cts.IsCancellationRequested)
                Console.WriteLine("Launcher exited prematurely or with errors. Check launcher logs for details.");
        }
    }
}