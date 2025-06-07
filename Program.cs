// Program.cs
using System;
using System.Collections.Generic;
using System.Diagnostics; // Required for Process related classes
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

// Specific using directives for your project's namespaces
using ObsidianLauncher; // For LauncherConfig
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using ObsidianLauncher.Utils;
// using ObsidianLauncher.Enums; // If you need direct access to enums here

public class Program
{
    private static readonly CancellationTokenSource _cts = new CancellationTokenSource();

    static async Task Main(string[] args)
    {
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Log.Warning("Cancellation requested via Ctrl+C.");
            _cts.Cancel();
            eventArgs.Cancel = true; // Prevent the process from terminating immediately if we want to clean up
        };

        LauncherConfig launcherConfig = null;
        try
        {
            launcherConfig = new LauncherConfig(); // Uses default path or can take one from args
            LoggerSetup.Initialize(launcherConfig);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Critical startup error: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            Console.Error.WriteLine("Ensure the application has permissions to create directories/files in its working path or the specified data path.");
            Environment.ExitCode = 1;
            return; // Exit if basic setup fails
        }

        Log.Information("==================================================");
        Log.Information("  Obsidian Launcher v{Version}",
            LauncherConfig.VERSION);
        Log.Information("==================================================");
        Log.Information("Data directory: {BaseDataPath}", launcherConfig.BaseDataPath);
        Log.Information("Log directory: {LogsDir}", launcherConfig.LogsDir);

        // --- Initialize Services ---
        using var httpManager = new HttpManager();
        var javaManager = new JavaManager(launcherConfig, httpManager);
        var assetManager = new AssetManager(launcherConfig, httpManager);
        var libraryManager = new LibraryManager(launcherConfig, httpManager);
        var argumentBuilder = new ArgumentBuilder(launcherConfig);
        var gameLauncher = new GameLauncher(launcherConfig);

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

            VersionManifest versionManifestAll = JsonSerializer.Deserialize<VersionManifest>(manifestJsonString,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (versionManifestAll?.Versions == null)
            {
                Log.Fatal("Failed to parse version manifest or no versions found.");
                return;
            }
            Log.Verbose("Version manifest JSON parsed successfully. Found {Count} versions.", versionManifestAll.Versions.Count);

            // --- Step 2: Select a Version and Get its Details ---
            string versionIdToLaunch = "1.20.4"; // Default
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                versionIdToLaunch = args[0];
                Log.Information("Overriding target version with command line argument: {VersionId}", versionIdToLaunch);
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
            JavaRuntimeInfo javaRuntime = await javaManager.EnsureJavaForMinecraftVersionAsync(minecraftVersion, _cts.Token);

            if (_cts.IsCancellationRequested) { Log.Warning("Java setup cancelled."); return; }

            if (javaRuntime == null)
            {
                Log.Error("Failed to obtain a suitable Java runtime for Minecraft version '{VersionId}'. Cannot proceed.", minecraftVersion.Id);
                return;
            }
            Log.Information("Java Runtime Ensured: {JavaExecutablePath}", javaRuntime.JavaExecutablePath);


            // --- Step 4: Download/Verify Assets ---
            Log.Information("--- Ensuring Assets for Minecraft {VersionId} ---", minecraftVersion.Id);
            var assetProgress = new Progress<AssetDownloadProgress>(report =>
            {
                if (report.ProcessedFiles % Math.Max(1, report.TotalFiles / 20) == 0 || report.ProcessedFiles == report.TotalFiles)
                {
                    Log.Information(
                        "[Assets] Progress: {Processed}/{Total} files ({OverallPercent:F1}%) - Current: {CurrentFile}",
                        report.ProcessedFiles, report.TotalFiles,
                        (report.TotalFiles > 0 ? (double)report.ProcessedFiles / report.TotalFiles * 100 : 0),
                        report.CurrentFile ?? "...");
                }
            });
            bool assetsOk = await assetManager.EnsureAssetsAsync(minecraftVersion, assetProgress, _cts.Token);

            if (_cts.IsCancellationRequested) { Log.Warning("Asset processing cancelled."); return; }
            if (!assetsOk)
            {
                Log.Error("Asset download or verification failed for version {VersionId}. Cannot proceed.", minecraftVersion.Id);
                return;
            }
            Log.Information("Assets Ensured for version {VersionId}", minecraftVersion.Id);

            // --- Step 5: Download Libraries & Extract Natives ---
            Log.Information("--- Ensuring Libraries for Minecraft {VersionId} ---", minecraftVersion.Id);
            string versionSpecificDir = Path.Combine(launcherConfig.VersionsDir, minecraftVersion.Id);
            Directory.CreateDirectory(versionSpecificDir);
            string nativesDirectory = Path.Combine(versionSpecificDir, $"{minecraftVersion.Id}-natives");
            Log.Information("Natives will be extracted to: {NativesDirectory}", nativesDirectory);

            var libraryProgress = new Progress<LibraryProcessingProgress>(report =>
            {
                if (report.Status.Contains("failed", StringComparison.OrdinalIgnoreCase) || report.Status.Contains("Skipped") || report.ProcessedLibraries % Math.Max(1, report.TotalLibraries / 10) == 0 || report.ProcessedLibraries == report.TotalLibraries)
                {
                    Log.Information("[Libs] {Processed}/{Total} - Status: {Status} - Lib: {LibraryName}",
                        report.ProcessedLibraries, report.TotalLibraries, report.Status, report.CurrentLibraryName);
                } else { Log.Verbose("[Libs] {Processed}/{Total} - Status: {Status} - Lib: {LibraryName}", report.ProcessedLibraries, report.TotalLibraries, report.Status, report.CurrentLibraryName); }
            });
            List<string> libraryClasspathEntries = await libraryManager.EnsureLibrariesAsync(minecraftVersion, nativesDirectory, libraryProgress, _cts.Token);

            if (_cts.IsCancellationRequested) { Log.Warning("Library processing cancelled."); return; }
            if (libraryClasspathEntries == null)
            {
                Log.Error("Failed to process one or more libraries for version {VersionId}. Cannot proceed.", minecraftVersion.Id);
                return;
            }
            Log.Information("All applicable libraries processed. Library classpath entries: {Count}", libraryClasspathEntries.Count);

            // --- Step 5.5: Download Client JAR ---
            Log.Information("--- Ensuring Client JAR for Minecraft {VersionId} ---", minecraftVersion.Id);
            string clientJarPath = Path.Combine(versionSpecificDir, $"{minecraftVersion.Id}.jar");
            bool clientJarOk = false;
            if (minecraftVersion.Downloads.TryGetValue("client", out DownloadDetails clientDownloadDetails))
            {
                clientJarOk = await assetManager.DownloadAndVerifyFileAsync(
                    clientDownloadDetails.Url, clientJarPath, clientDownloadDetails.Sha1,
                    $"Client JAR for {minecraftVersion.Id}", _cts.Token, clientDownloadDetails.Size);
            }
            else { Log.Error("No client JAR download information found for version {VersionId}.", minecraftVersion.Id); }

            if (_cts.IsCancellationRequested) { Log.Warning("Client JAR download cancelled."); return; }
            if (!clientJarOk)
            {
                Log.Error("Failed to download or verify client JAR for version {VersionId}. Cannot proceed.", minecraftVersion.Id);
                return;
            }
            Log.Information("Client JAR for version {VersionId} is ready at {ClientJarPath}", minecraftVersion.Id, clientJarPath);

            // --- Step 6: Construct Classpath ---
            Log.Information("--- Constructing Classpath ---");
            string classpathString = argumentBuilder.BuildClasspath(clientJarPath, libraryClasspathEntries);
            // BuildClasspath already logs details.

            // --- Step 7: Construct JVM Arguments ---
            Log.Information("--- Constructing JVM Arguments ---");
            // TODO: Populate these from a real auth flow / settings
            argumentBuilder.SetOfflinePlayerName("Player123");
            // Example of setting a feature flag if needed by arguments:
            // argumentBuilder.SetFeatureFlag("is_demo_user", true);
            // argumentBuilder.SetFeatureFlag("has_custom_resolution", true);
            // argumentBuilder.SetCustomResolution(1280, 720);


            List<string> jvmArgs = argumentBuilder.BuildJvmArguments(minecraftVersion, classpathString, Path.GetFullPath(nativesDirectory), javaRuntime);

            // --- Step 8: Construct Game Arguments ---
            Log.Information("--- Constructing Game Arguments ---");
            List<string> gameArgs = argumentBuilder.BuildGameArguments(minecraftVersion);

            // --- Step 9: Launch Minecraft ---
            Log.Information("--- Launching Minecraft {VersionId} ---", minecraftVersion.Id);
            string gameWorkingDirectory = Path.GetFullPath(launcherConfig.BaseDataPath); // Or a version-specific instance directory like `versionSpecificDir`
            // For isolated instances: string gameWorkingDirectory = versionSpecificDir;
            Log.Information("Game working directory set to: {GameDir}", gameWorkingDirectory);


            int exitCode = await gameLauncher.LaunchAsync(
                javaRuntime.JavaExecutablePath,
                jvmArgs,
                minecraftVersion.MainClass,
                gameArgs,
                gameWorkingDirectory,
                _cts.Token
            );

            if (_cts.IsCancellationRequested)
            {
                Log.Warning("Minecraft launch was explicitly cancelled by the user during execution.");
            }
            else
            {
                Log.Information("Minecraft process finished with exit code: {ExitCode}", exitCode);
                if (exitCode != 0)
                {
                    Log.Warning("Minecraft exited with a non-zero exit code ({ExitCode}), indicating a potential issue or crash. Check Minecraft's own logs if created.", exitCode);
                }
            }

            Log.Information("Obsidian Launcher has completed its operation for version {VersionId}.", minecraftVersion.Id);
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
                 Console.WriteLine("Launcher exited prematurely or with errors. Check logs for details.");
            }
        }
    }
}