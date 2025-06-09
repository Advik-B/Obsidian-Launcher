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

        LauncherConfig? launcherConfig = null;
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

        Log.Information("==================================================");
        Log.Information("  Obsidian Launcher {Version}", $"v{LauncherConfig.VERSION}");
        Log.Information("==================================================");
        Log.Information("Global data directory: {BaseDataPath}", launcherConfig.BaseDataPath);
        Log.Information("Instances root directory: {InstancesRootDir}", launcherConfig.InstancesRootDir);
        Log.Information("Launcher log directory: {LogsDir}", launcherConfig.LogsDir);

        using var httpManager = new HttpManager();
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
            string? cliInstanceNameFromArg = null;
            string? cliPlayerNameFromArg = null; // This will now be the session player name

            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                versionIdToLaunch = args[0];
                Log.Information("Target version from command line: {VersionId}", versionIdToLaunch);
                if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
                {
                    cliInstanceNameFromArg = args[1];
                    Log.Information("Target instance name from command line: {InstanceName}", cliInstanceNameFromArg);
                }

                // Player name is now the 3rd argument if instance name is also provided
                if (args.Length > 2 && !string.IsNullOrWhiteSpace(args[2]))
                {
                    cliPlayerNameFromArg = args[2];
                    Log.Information("Player name for this session from command line: {PlayerName}",
                        cliPlayerNameFromArg);
                }
            }

            Log.Information("Target Minecraft version for setup: {VersionId}", versionIdToLaunch);

            // Determine the player name for this session
            var sessionPlayerName = cliPlayerNameFromArg ?? $"Player{Random.Shared.Next(100, 999)}";
            Log.Information("Using player name for this session: {PlayerName}", sessionPlayerName);
            argumentBuilder.SetOfflinePlayerName(sessionPlayerName); // Set player name for argument builder


            var selectedVersionMeta = versionManifestAll.Versions.FirstOrDefault(v => v.Id == versionIdToLaunch);
            if (selectedVersionMeta == null || string.IsNullOrEmpty(selectedVersionMeta.Url))
            {
                Log.Error("Target version '{VersionId}' not found in manifest or URL is missing.", versionIdToLaunch);
                return;
            }

            Log.Information("Found URL for version '{VersionId}': {Url}", versionIdToLaunch, selectedVersionMeta.Url);

            Log.Information("Fetching details for version '{VersionId}'...", versionIdToLaunch);
            var versionDetailsResponseMsg =
                await httpManager.GetAsync(selectedVersionMeta.Url, cancellationToken: _cts.Token);

            if (_cts.IsCancellationRequested)
            {
                Log.Warning("Version details fetch cancelled.");
                return;
            }

            if (!versionDetailsResponseMsg.IsSuccessStatusCode)
            {
                var errorContent = await versionDetailsResponseMsg.Content.ReadAsStringAsync(_cts.Token);
                Log.Error(
                    "Failed to fetch version details for '{VersionId}'. Status: {StatusCode}, URL: {Url}, Error: {ErrorContent}",
                    versionIdToLaunch, versionDetailsResponseMsg.StatusCode,
                    versionDetailsResponseMsg.RequestMessage?.RequestUri, errorContent);
                return;
            }

            var versionDetailsJsonString = await versionDetailsResponseMsg.Content.ReadAsStringAsync(_cts.Token);
            Log.Information("Successfully fetched version details for '{VersionId}'. Size: {Length} bytes",
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

            var instanceNameToUse = cliInstanceNameFromArg ?? $"Vanilla_{minecraftVersion.Id}";

            var assetProgress = new Progress<AssetDownloadProgress>(report =>
            {
                if (report.ProcessedFiles % Math.Max(1, report.TotalFiles / 20) == 0 ||
                    report.ProcessedFiles == report.TotalFiles)
                    Log.Information(
                        "[Assets] Progress: {Processed}/{Total} files ({OverallPercent:F1}%) - Current: {CurrentFile}",
                        report.ProcessedFiles, report.TotalFiles,
                        report.TotalFiles > 0 ? (double)report.ProcessedFiles / report.TotalFiles * 100 : 0,
                        report.CurrentFile ?? "...");
            });
            var libraryProgress = new Progress<LibraryProcessingProgress>(report =>
            {
                if (report.Status.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                    report.Status.Contains("Skipped") ||
                    report.ProcessedLibraries % Math.Max(1, report.TotalLibraries / 10) == 0 ||
                    report.ProcessedLibraries == report.TotalLibraries)
                    Log.Information("[Libs] {Processed}/{Total} - Status: {Status} - Lib: {LibraryName}",
                        report.ProcessedLibraries, report.TotalLibraries, report.Status, report.CurrentLibraryName);
                else
                    Log.Verbose("[Libs] {Processed}/{Total} - Status: {Status} - Lib: {LibraryName}",
                        report.ProcessedLibraries, report.TotalLibraries, report.Status, report.CurrentLibraryName);
            });

            var (currentInstance, clientJarPath, libraryClasspathEntries) =
                await instanceManager.GetOrCreateInstanceAsync(
                    instanceNameToUse,
                    minecraftVersion.Id,
                    minecraftVersion,
                    // PlayerName no longer passed here for instance creation/storage
                    assetProgress,
                    libraryProgress,
                    _cts.Token);

            if (currentInstance == null || string.IsNullOrEmpty(clientJarPath) || libraryClasspathEntries == null)
            {
                Log.Fatal("Failed to get, create, or sync instance '{InstanceName}'. Cannot proceed.",
                    instanceNameToUse);
                return;
            }

            Log.Information("Instance ready: '{InstanceName}' (ID: {InstanceId}) at {InstancePath}",
                currentInstance.Name, currentInstance.Id, currentInstance.InstancePath);
            Log.Information("Instance Playtime - Total: {TotalPlaytime}, Last Session: {LastSessionPlaytime}",
                currentInstance.TotalPlaytime.ToString(@"d\.hh\:mm\:ss"),
                currentInstance.LastSessionPlaytime.ToString(@"hh\:mm\:ss"));

            Log.Information("--- Ensuring Java Runtime for Minecraft {VersionId} ---", minecraftVersion.Id);
            JavaRuntimeInfo? javaRuntime;
            if (!string.IsNullOrEmpty(currentInstance.CustomJavaRuntimePath))
            {
                var customJavaDir = Path.GetFullPath(currentInstance.CustomJavaRuntimePath);
                Log.Information("Instance '{InstanceName}' specifies custom Java path: {CustomJavaPath}",
                    currentInstance.Name, customJavaDir);
                var assumedBinPath = Path.Combine(customJavaDir, "bin");
                var javaExeName = OsUtils.GetCurrentOS() == OperatingSystemType.Windows ? "javaw.exe" : "java";
                var customJavaExePath = Path.Combine(assumedBinPath, javaExeName);

                if (File.Exists(customJavaExePath))
                {
                    javaRuntime = new JavaRuntimeInfo
                    {
                        JavaExecutablePath = customJavaExePath,
                        HomePath = customJavaDir,
                        ComponentName = "custom",
                        MajorVersion = 0,
                        Source = "instance_custom"
                    };
                    Log.Information("Using custom Java runtime specified by instance: {JavaPath}", customJavaExePath);
                }
                else
                {
                    Log.Warning(
                        "Custom Java path '{CustomJavaDir}' specified by instance, but executable not found at '{ExpectedExePath}'. Falling back to globally managed Java.",
                        customJavaDir, customJavaExePath);
                    javaRuntime = await javaManager.EnsureJavaForMinecraftVersionAsync(minecraftVersion, _cts.Token);
                }
            }
            else
            {
                javaRuntime = await javaManager.EnsureJavaForMinecraftVersionAsync(minecraftVersion, _cts.Token);
            }

            if (_cts.IsCancellationRequested)
            {
                Log.Warning("Java setup cancelled.");
                return;
            }

            if (javaRuntime == null)
            {
                Log.Error(
                    "Failed to obtain a suitable Java runtime for Minecraft version '{VersionId}'. Cannot proceed.",
                    minecraftVersion.Id);
                return;
            }

            Log.Information("Java Runtime Ensured: {JavaExecutablePath}", javaRuntime.JavaExecutablePath);

            Log.Information("--- Constructing Classpath ---");
            var classpathString = argumentBuilder.BuildClasspath(clientJarPath, libraryClasspathEntries);

            Log.Information("--- Constructing JVM Arguments ---");
            var jvmArgs = argumentBuilder.BuildJvmArguments(
                minecraftVersion,
                classpathString,
                Path.GetFullPath(currentInstance.NativesPath),
                javaRuntime,
                currentInstance.InstancePath
            );
            if (currentInstance.CustomJvmArguments != null && currentInstance.CustomJvmArguments.Any())
            {
                Log.Information("Applying {Count} custom JVM arguments from instance '{InstanceName}'...",
                    currentInstance.CustomJvmArguments.Count, currentInstance.Name);
                jvmArgs.AddRange(currentInstance.CustomJvmArguments);
            }

            Log.Information("--- Constructing Game Arguments ---");
            var gameArgs = argumentBuilder.BuildGameArguments(
                minecraftVersion,
                currentInstance.InstancePath
            );

            Log.Information(
                "--- Launching Minecraft {VersionId} for instance '{InstanceName}' (Player: {PlayerName}) ---",
                minecraftVersion.Id, currentInstance.Name, sessionPlayerName);
            var gameWorkingDirectory = Path.GetFullPath(currentInstance.GameDataPath);
            Log.Information("Game working directory (instance path) set to: {GameDir}", gameWorkingDirectory);

            var sessionStartTime = DateTime.UtcNow;
            var exitCode = await gameLauncher.LaunchAsync(
                javaRuntime.JavaExecutablePath,
                jvmArgs,
                minecraftVersion.MainClass,
                gameArgs,
                gameWorkingDirectory,
                _cts.Token
            );
            var sessionDuration = DateTime.UtcNow - sessionStartTime;
            await instanceManager.UpdateLastPlayedAsync(currentInstance, sessionDuration);

            if (_cts.IsCancellationRequested)
            {
                Log.Warning("Minecraft launch was explicitly cancelled by the user during execution.");
            }
            else
            {
                Log.Information("Minecraft process finished with exit code: {ExitCode}", exitCode);
                Log.Information(
                    "Instance '{InstanceName}' - Session Playtime: {SessionPlaytimeFormat}, Total Playtime: {TotalPlaytimeFormat}",
                    currentInstance.Name, sessionDuration.ToString(@"hh\:mm\:ss"),
                    currentInstance.TotalPlaytime.ToString(@"d\.hh\:mm\:ss"));
                if (exitCode != 0)
                    Log.Warning(
                        "Minecraft exited with a non-zero exit code ({ExitCode}), check instance logs: {InstanceLogPath}",
                        exitCode, Path.Combine(currentInstance.GameDataPath, "logs"));
            }

            Log.Information(
                "Obsidian Launcher has completed its operation for instance '{InstanceName}' (Version {VersionId}).",
                currentInstance.Name, minecraftVersion.Id);
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