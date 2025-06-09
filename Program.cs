// Program.cs
using System;
using System.Collections.Generic;
using System.Diagnostics; 
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

using ObsidianLauncher;
using ObsidianLauncher.Enums;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using ObsidianLauncher.Utils;

public class Program
{
    private static readonly CancellationTokenSource _cts = new CancellationTokenSource();
    
    static async Task Main(string[] args)
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

        Log.Information("==================================================");
        Log.Information("  Obsidian Launcher {Version}", $"v{LauncherConfig.VERSION}");
        Log.Information("==================================================");
        Log.Information("Global data directory: {BaseDataPath}", launcherConfig.BaseDataPath);
        Log.Information("Instances root directory: {InstancesRootDir}", launcherConfig.InstancesRootDir);
        Log.Information("Launcher log directory: {LogsDir}", launcherConfig.LogsDir);

        using var httpManager = new HttpManager();
        var javaManager = new JavaManager(launcherConfig, httpManager);
        var assetManager = new AssetManager(launcherConfig, httpManager);     // Initialized for InstanceManager
        var libraryManager = new LibraryManager(launcherConfig, httpManager); // Initialized for InstanceManager
        var instanceManager = new InstanceManager(launcherConfig, assetManager, libraryManager); // Pass dependencies
        var argumentBuilder = new ArgumentBuilder(launcherConfig); 
        var gameLauncher = new GameLauncher(launcherConfig);

        try
        {
            Log.Information("Fetching Minecraft version manifest from Mojang...");
            HttpResponseMessage manifestResponseMsg = await httpManager.GetAsync(
                "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json",
                cancellationToken: _cts.Token);

            if (_cts.IsCancellationRequested) { Log.Warning("Manifest fetch cancelled."); return; }

            if (!manifestResponseMsg.IsSuccessStatusCode)
            {
                // ... (error handling) ...
                string errorContent = await manifestResponseMsg.Content.ReadAsStringAsync(_cts.Token);
                Log.Fatal("Failed to fetch version manifest. Status: {StatusCode}, URL: {Url}, Error: {ErrorContent}",
                    manifestResponseMsg.StatusCode, manifestResponseMsg.RequestMessage?.RequestUri, errorContent);
                return;
            }

            string manifestJsonString = await manifestResponseMsg.Content.ReadAsStringAsync(_cts.Token);
            Log.Information("Successfully fetched version manifest (status {StatusCode}). Size: {Length} bytes",
                manifestResponseMsg.StatusCode, manifestJsonString.Length);

            VersionManifest versionManifestAll = JsonSerializer.Deserialize<VersionManifest>(manifestJsonString,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (versionManifestAll?.Versions == null)
            {
                // ... (error handling) ...
                Log.Fatal("Failed to parse version manifest or no versions found.");
                return;
            }
            Log.Verbose("Version manifest JSON parsed successfully. Found {Count} versions.", versionManifestAll.Versions.Count);

            string versionIdToLaunch = "1.20.4"; 
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
                    Log.Information("Target player name from command line: {PlayerName}", cliPlayerNameFromArg);
                }
            }
            Log.Information("Target Minecraft version for setup: {VersionId}", versionIdToLaunch);


            var selectedVersionMeta = versionManifestAll.Versions.FirstOrDefault(v => v.Id == versionIdToLaunch);
            if (selectedVersionMeta == null || string.IsNullOrEmpty(selectedVersionMeta.Url))
            {
                // ... (error handling) ...
                Log.Error("Target version '{VersionId}' not found in manifest or URL is missing.", versionIdToLaunch);
                return;
            }
            // ... (fetch version details as before) ...
            Log.Information("Fetching details for version '{VersionId}'...", versionIdToLaunch);
            HttpResponseMessage versionDetailsResponseMsg = await httpManager.GetAsync(selectedVersionMeta.Url, cancellationToken: _cts.Token);
            // ... (error check and parse MinecraftVersion mcVersion) ...
            if (_cts.IsCancellationRequested) { Log.Warning("Version details fetch cancelled."); return; }
            if (!versionDetailsResponseMsg.IsSuccessStatusCode) { /* ... */ return; }
            string versionDetailsJsonString = await versionDetailsResponseMsg.Content.ReadAsStringAsync(_cts.Token);
            MinecraftVersion minecraftVersion = JsonSerializer.Deserialize<MinecraftVersion>(versionDetailsJsonString,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (minecraftVersion == null) { /* ... */ return; }
            Log.Information("Successfully parsed Minecraft version object: {Id} (Type: {Type})", minecraftVersion.Id, minecraftVersion.Type);
            
            // --- Instance Setup & Sync ---
            string instanceNameToUse = cliInstanceNameFromArg ?? $"Vanilla_{minecraftVersion.Id}"; 
            string effectivePlayerName = cliPlayerNameFromArg ?? $"Player{Random.Shared.Next(100, 999)}";
            
            var assetProgress = new Progress<AssetDownloadProgress>(report => {/* ... */});
            var libraryProgress = new Progress<LibraryProcessingProgress>(report => {/* ... */});

            var (currentInstance, clientJarPath, libraryClasspathEntries) = 
                await instanceManager.GetOrCreateInstanceAsync(
                    instanceNameToUse, 
                    minecraftVersion.Id, 
                    minecraftVersion,
                    effectivePlayerName,
                    assetProgress,
                    libraryProgress,
                    _cts.Token);

            if (currentInstance == null || string.IsNullOrEmpty(clientJarPath) || libraryClasspathEntries == null)
            {
                Log.Fatal("Failed to get, create, or sync instance '{InstanceName}'. Cannot proceed.", instanceNameToUse);
                return;
            }
            Log.Information("Instance ready: '{InstanceName}' (ID: {InstanceId}) at {InstancePath} for player '{PlayerName}'", 
                currentInstance.Name, currentInstance.Id, currentInstance.InstancePath, currentInstance.PlayerName);
            Log.Information("Instance Playtime - Total: {TotalPlaytime}, Last Session: {LastSessionPlaytime}",
                currentInstance.TotalPlaytime.ToString(@"d\.hh\:mm\:ss"), currentInstance.LastSessionPlaytime.ToString(@"hh\:mm\:ss"));
            
            argumentBuilder.SetOfflinePlayerName(currentInstance.PlayerName); 

            // --- Java Runtime ---
            Log.Information("--- Ensuring Java Runtime for Minecraft {VersionId} ---", minecraftVersion.Id);
            JavaRuntimeInfo javaRuntime;
             if (!string.IsNullOrEmpty(currentInstance.CustomJavaRuntimePath))
            {
                string customJavaDir = Path.GetFullPath(currentInstance.CustomJavaRuntimePath);
                string javaExeName = OsUtils.GetCurrentOS() == OperatingSystemType.Windows ? "javaw.exe" : "java";
                string customJavaExePath = Path.Combine(customJavaDir, "bin", javaExeName);
                if(File.Exists(customJavaExePath))
                {
                    javaRuntime = new JavaRuntimeInfo { /* ... */ JavaExecutablePath = customJavaExePath, HomePath = customJavaDir, Source = "instance_custom"};
                }
                else
                {
                     Log.Warning("Custom Java path '{CustomJavaDir}' specified by instance, but executable not found. Falling back to globally managed Java.", customJavaDir);
                    javaRuntime = await javaManager.EnsureJavaForMinecraftVersionAsync(minecraftVersion, _cts.Token);
                }
            }
            else
            {
                 javaRuntime = await javaManager.EnsureJavaForMinecraftVersionAsync(minecraftVersion, _cts.Token);
            }
            if (javaRuntime == null) { /* ... */ return; }
            Log.Information("Java Runtime Ensured: {JavaExecutablePath}", javaRuntime.JavaExecutablePath);


            // Assets, Client JAR, Libraries are now handled by InstanceManager.Sync
            // `clientJarPath` and `libraryClasspathEntries` are returned by GetOrCreateInstanceAsync

            // --- Construct Classpath ---
            Log.Information("--- Constructing Classpath ---");
            string classpathString = argumentBuilder.BuildClasspath(clientJarPath, libraryClasspathEntries);

            // --- Construct JVM Arguments ---
            Log.Information("--- Constructing JVM Arguments ---");
            List<string> jvmArgs = argumentBuilder.BuildJvmArguments(
                minecraftVersion, 
                classpathString, 
                Path.GetFullPath(currentInstance.NativesPath), 
                javaRuntime, 
                currentInstance.InstancePath 
            );
            if (currentInstance.CustomJvmArguments != null && currentInstance.CustomJvmArguments.Any())
            {
                Log.Information("Applying {Count} custom JVM arguments from instance '{InstanceName}'...", currentInstance.CustomJvmArguments.Count, currentInstance.Name);
                jvmArgs.AddRange(currentInstance.CustomJvmArguments); 
            }

            // --- Construct Game Arguments ---
            Log.Information("--- Constructing Game Arguments ---");
            List<string> gameArgs = argumentBuilder.BuildGameArguments(
                minecraftVersion, 
                currentInstance.InstancePath 
            );

            // --- Launch Minecraft ---
            Log.Information("--- Launching Minecraft {VersionId} for instance '{InstanceName}' ---", minecraftVersion.Id, currentInstance.Name);
            string gameWorkingDirectory = Path.GetFullPath(currentInstance.GameDataPath); 
            Log.Information("Game working directory (instance path) set to: {GameDir}", gameWorkingDirectory);

            DateTime sessionStartTime = DateTime.UtcNow; 
            int exitCode = await gameLauncher.LaunchAsync(
                javaRuntime.JavaExecutablePath,
                jvmArgs,
                minecraftVersion.MainClass,
                gameArgs,
                gameWorkingDirectory,
                _cts.Token
            );
            TimeSpan sessionDuration = DateTime.UtcNow - sessionStartTime; 
            await instanceManager.UpdateLastPlayedAsync(currentInstance, sessionDuration); 

            if (_cts.IsCancellationRequested)
            {
                Log.Warning("Minecraft launch was explicitly cancelled by the user during execution.");
            }
            else
            {
                Log.Information("Minecraft process finished with exit code: {ExitCode}", exitCode);
                Log.Information("Instance '{InstanceName}' - Session Playtime: {SessionPlaytimeFormat}, Total Playtime: {TotalPlaytimeFormat}",
                    currentInstance.Name, sessionDuration.ToString(@"hh\:mm\:ss"), currentInstance.TotalPlaytime.ToString(@"d\.hh\:mm\:ss"));
                if (exitCode != 0)
                {
                    Log.Warning("Minecraft exited with a non-zero exit code ({ExitCode}), check instance logs: {InstanceLogPath}", exitCode, Path.Combine(currentInstance.GameDataPath, "logs"));
                }
            }

            Log.Information("Obsidian Launcher has completed its operation for instance '{InstanceName}' (Version {VersionId}).", currentInstance.Name, minecraftVersion.Id);
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
            {
                 Console.WriteLine("Launcher exited prematurely or with errors. Check launcher logs for details.");
            }
        }
    }
}