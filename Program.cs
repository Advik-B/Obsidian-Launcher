// Program.cs
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading; // For CancellationToken
using System.Threading.Tasks;
using Serilog;

// Specific using directives for your project's namespaces
using ObsidianLauncher; // For LauncherConfig
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using ObsidianLauncher.Utils;
// using ObsidianLauncher.Enums; // If you need direct access to enums here

namespace ObsidianLauncher;

public class Program
{
    // Optional: Create a CancellationTokenSource if you want to enable cancellation
    // for long-running operations (like downloads) via Ctrl+C or other means.
    private static readonly CancellationTokenSource _cts = new();

    static async Task Main(string[] args)
    {
        // Setup Ctrl+C handling to gracefully cancel operations
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Log.Warning("Cancellation requested via Ctrl+C.");
            _cts.Cancel();
            eventArgs.Cancel = true; // Prevent the process from terminating immediately
        };

        LauncherConfig launcherConfig = null;
        try
        {
            launcherConfig = new LauncherConfig(); // Uses default path or can take one from args
            LoggerSetup.Initialize(launcherConfig); // Initialize Serilog
        }
        catch (Exception ex)
        {
            // If config or logger setup fails, we can only log to console.
            Console.Error.WriteLine($"Critical startup error: {ex.Message}");
            Console.Error.WriteLine("Ensure the application has permissions to create directories/files in its working path or the specified data path.");
            return; // Exit if basic setup fails
        }


        Log.Information("==================================================");
        Log.Information("  Obsidian Minecraft Launcher (C# Port) v0.1");
        Log.Information("==================================================");
        Log.Information("Data directory: {BaseDataPath}", launcherConfig.BaseDataPath);
        Log.Information("Log directory: {LogsDir}", launcherConfig.LogsDir);

        // --- Initialize Services ---
        // HttpManager is designed with a static HttpClient, so it's okay to create an instance
        // or you could register it with a DI container if the project grows.
        using var httpManager = new HttpManager();
        var javaManager = new JavaManager(launcherConfig, httpManager);
        // Other services would be initialized here as needed (e.g., AssetManager, LaunchService)

        try
        {
            // --- Step 1: Fetch and Parse Version Manifest ---
            Log.Information("Fetching Minecraft version manifest from Mojang...");
            HttpResponseMessage manifestResponseMsg = await httpManager.GetAsync(
                "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json",
                cancellationToken: _cts.Token);

            if (_cts.IsCancellationRequested) { Log.Warning("Manifest fetch cancelled."); return; }

            if (!manifestResponseMsg.IsSuccessStatusCode)
            {
                string errorContent = await manifestResponseMsg.Content.ReadAsStringAsync(_cts.Token);
                Log.Fatal("Failed to fetch version manifest. Status: {StatusCode}, URL: {Url}, Error: {ErrorContent}",
                    manifestResponseMsg.StatusCode, manifestResponseMsg.RequestMessage?.RequestUri, errorContent);
                return;
            }

            string manifestJsonString = await manifestResponseMsg.Content.ReadAsStringAsync(_cts.Token);
            Log.Information("Successfully fetched version manifest (status {StatusCode}). Size: {Length} bytes",
                manifestResponseMsg.StatusCode, manifestJsonString.Length);

            VersionManifest? versionManifestAll = JsonSerializer.Deserialize<VersionManifest>(
                manifestJsonString,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

            if (versionManifestAll?.Versions == null)
            {
                Log.Fatal("Failed to parse version manifest or no versions found.");
                return;
            }
            Log.Verbose("Version manifest JSON parsed successfully. Found {Count} versions.", versionManifestAll.Versions.Count);

            // --- Step 2: Select a Version and Get its Details ---
            string versionIdToLaunch = "1.20.4"; // Example: Or get from args, config, or user input
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])) {
                versionIdToLaunch = args[0];
            }
            Log.Information("Target Minecraft version for setup: {VersionId}", versionIdToLaunch);


            var selectedVersionMeta = versionManifestAll.Versions.FirstOrDefault(v => v.Id == versionIdToLaunch);
            if (selectedVersionMeta == null || string.IsNullOrEmpty(selectedVersionMeta.Url))
            {
                Log.Error("Target version '{VersionId}' not found in manifest or URL is missing.", versionIdToLaunch);
                return;
            }
            Log.Information("Found URL for version '{VersionId}': {Url}", versionIdToLaunch, selectedVersionMeta.Url);

            Log.Information("Fetching details for version '{VersionId}'...", versionIdToLaunch);
            HttpResponseMessage versionDetailsResponseMsg = await httpManager.GetAsync(selectedVersionMeta.Url, cancellationToken: _cts.Token);

            if (_cts.IsCancellationRequested) { Log.Warning("Version details fetch cancelled."); return; }

            if (!versionDetailsResponseMsg.IsSuccessStatusCode)
            {
                string errorContent = await versionDetailsResponseMsg.Content.ReadAsStringAsync(_cts.Token);
                Log.Error("Failed to fetch version details for '{VersionId}'. Status: {StatusCode}, URL: {Url}, Error: {ErrorContent}",
                    versionIdToLaunch, versionDetailsResponseMsg.StatusCode, versionDetailsResponseMsg.RequestMessage?.RequestUri, errorContent);
                return;
            }
            string versionDetailsJsonString = await versionDetailsResponseMsg.Content.ReadAsStringAsync(_cts.Token);
            Log.Information("Successfully fetched version details for '{VersionId}'. Size: {Length} bytes",
                versionIdToLaunch, versionDetailsJsonString.Length);

            MinecraftVersion minecraftVersion = JsonSerializer.Deserialize<MinecraftVersion>(versionDetailsJsonString,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (minecraftVersion == null)
            {
                Log.Fatal("Failed to parse details for version '{VersionId}'.", versionIdToLaunch);
                return;
            }
            Log.Information("Successfully parsed Minecraft version object: {Id} (Type: {Type})", minecraftVersion.Id, minecraftVersion.Type);

            // --- Step 3: Ensure Java Runtime ---
            Log.Information("--- Ensuring Java Runtime for Minecraft {VersionId} ---", minecraftVersion.Id);
            if (minecraftVersion.JavaVersion != null)
            {
                Log.Information("Minecraft requires Java: Component='{Component}', MajorVersion='{MajorVersion}'",
                    minecraftVersion.JavaVersion.Component, minecraftVersion.JavaVersion.MajorVersion);
            }
            else
            {
                Log.Warning("Minecraft version {VersionId} does not specify a Java version. Attempting to find a compatible system Java or a default one if configured.", minecraftVersion.Id);
                // Potentially add logic here to find system Java or use a default if mcVersion.JavaVersion is null
                // For now, the EnsureJavaForMinecraftVersionAsync will handle the null case by returning null.
            }

            JavaRuntimeInfo javaRuntime = await javaManager.EnsureJavaForMinecraftVersionAsync(minecraftVersion, _cts.Token);

            if (_cts.IsCancellationRequested) { Log.Warning("Java setup cancelled."); return; }

            if (javaRuntime != null)
            {
                Log.Information("Successfully ensured Java runtime for '{VersionId}'.", minecraftVersion.Id);
                Log.Information("  Java Home: {HomePath}", javaRuntime.HomePath);
                Log.Information("  Java Executable: {JavaExecutablePath}", javaRuntime.JavaExecutablePath);
                Log.Information("  Source: {Source}, Component: {Component}, Version: {MajorVersion}",
                    javaRuntime.Source, javaRuntime.ComponentName, javaRuntime.MajorVersion);
            }
            else
            {
                Log.Error("Failed to obtain a suitable Java runtime for Minecraft version '{VersionId}'. Cannot proceed with launch.", minecraftVersion.Id);
                // In a real launcher, you might prompt the user or guide them.
                return;
            }

            // --- Step 4: (TODO) Download Assets ---
            Log.Information("--- Asset Download/Verification (Placeholder) ---");
            // AssetIndex assetIndex = minecraftVersion.AssetIndex;
            // if (assetIndex != null)
            // {
            //     Log.Information("Asset Index ID: {Id}, URL: {Url}", assetIndex.Id, assetIndex.Url);
            //     // 1. Download assetIndex.Url if it doesn't exist or SHA1 mismatch
            //     // 2. Parse the asset index JSON
            //     // 3. Iterate through asset objects, download if missing/corrupt to config.AssetObjectsDir
            // }
            // else
            // {
            //     Log.Warning("No assetIndex specified for version {VersionId}.", minecraftVersion.Id);
            // }

            // --- Step 5: (TODO) Download Libraries ---
            Log.Information("--- Library Download/Verification (Placeholder) ---");
            // foreach (var lib in minecraftVersion.Libraries)
            // {
            //     // 1. Check rules (OsUtils, feature flags)
            //     // 2. If applicable, download lib.Downloads.Artifact.Url to config.LibrariesDir/lib.Downloads.Artifact.Path
            //     // 3. Verify SHA1
            //     // 4. If it's a native library (check lib.Natives), download the correct classifier
            //     // 5. Extract native JAR to natives directory using lib.Extract rules
            // }

            // --- Step 6: (TODO) Construct Classpath ---
            Log.Information("--- Classpath Construction (Placeholder) ---");
            // List<string> classpathEntries = new List<string>();
            // Add client JAR (minecraftVersion.Downloads["client"].Path relative to versionsDir)
            // Add all applicable library JARs

            // --- Step 7: (TODO) Construct JVM Arguments ---
            Log.Information("--- JVM Argument Construction (Placeholder) ---");
            // List<string> jvmArgs = new List<string>();
            // Process minecraftVersion.Arguments.Jvm (handle plain strings and conditional rules)
            // Replace placeholders: ${natives_directory}, ${launcher_name}, ${launcher_version},
            //                     ${classpath}, ${auth_player_name}, etc.
            // Add main class: minecraftVersion.MainClass
            // Add client logging argument if present: minecraftVersion.Logging.Client.Argument (replace ${path})

            // --- Step 8: (TODO) Construct Game Arguments ---
            Log.Information("--- Game Argument Construction (Placeholder) ---");
            // List<string> gameArgs = new List<string>();
            // Process minecraftVersion.Arguments.Game or minecraftVersion.MinecraftArguments
            // Replace placeholders

            // --- Step 9: (TODO) Launch Minecraft ---
            Log.Information("--- Launching Minecraft (Placeholder) ---");
            // ProcessStartInfo startInfo = new ProcessStartInfo
            // {
            //     FileName = javaRuntime.JavaExecutablePath,
            //     Arguments = string.Join(" ", jvmArgs) + " " + string.Join(" ", gameArgs),
            //     WorkingDirectory = launcherConfig.BaseDataPath // Or a specific game instance directory
            //     // ... other settings like RedirectStandardOutput, UseShellExecute = false
            // };
            // Log.Information("Launch command: {FileName} {Arguments}", startInfo.FileName, startInfo.Arguments);
            // Process.Start(startInfo);

            Log.Information("Minecraft Launcher (C# Port) simulated run finished.");
        }
        catch (OperationCanceledException)
        {
            Log.Warning("A long-running operation was cancelled by the user or a timeout.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "An unhandled exception occurred in the main application flow.");
        }
        finally
        {
            Log.Information("Shutting down logger...");
            await Log.CloseAndFlushAsync(); // Ensure all logs are written, especially for file sink
        }
    }
}