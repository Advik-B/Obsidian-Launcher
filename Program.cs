// Program.cs
using System;
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
            Console.Error.WriteLine("Ensure the application has permissions to create directories/files in its working path or the specified data path.");
            return;
        }

        Log.Information("==================================================");
        Log.Information("  Obsidian Minecraft Launcher (C# Port) v0.1");
        Log.Information("==================================================");
        Log.Information("Data directory: {BaseDataPath}", launcherConfig.BaseDataPath);
        Log.Information("Log directory: {LogsDir}", launcherConfig.LogsDir);

        using var httpManager = new HttpManager();
        var javaManager = new JavaManager(launcherConfig, httpManager); // Renamed
        var assetManager = new AssetManager(launcherConfig, httpManager); // New
        // var libraryManager = new LibraryManager(launcherConfig, httpManager); // Placeholder for future
        // var gameLauncher = new GameLauncher(launcherConfig); // Placeholder for future

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
            // string versionIdToLaunch = "rd-132328"; // For testing old version
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
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
                Log.Warning("Minecraft version {VersionId} does not explicitly specify a Java version. The launcher will attempt to use a default or system Java if necessary (not yet implemented).", minecraftVersion.Id);
                // For older versions without javaVersion, you might need a default (e.g. Java 8)
                // or prompt the user, or try to find system Java.
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
                Log.Error("Failed to obtain a suitable Java runtime for Minecraft version '{VersionId}'. Cannot proceed.", minecraftVersion.Id);
                return;
            }

            // --- Step 4: Download/Verify Assets ---
            Log.Information("--- Ensuring Assets for Minecraft {VersionId} ---", minecraftVersion.Id);
            var assetProgress = new Progress<AssetDownloadProgress>(progressReport =>
            {
                // Basic progress reporting. Could be more sophisticated for a UI.
                double overallProgressPercent = (progressReport.TotalFiles > 0)
                    ? (double)progressReport.ProcessedFiles / progressReport.TotalFiles * 100
                    : 0;
                // Log only occasionally to avoid spamming, e.g., every 5% or every N files
                if (progressReport.ProcessedFiles % Math.Max(1, progressReport.TotalFiles / 20) == 0 || progressReport.ProcessedFiles == progressReport.TotalFiles)
                {
                    Log.Information(
                        "[Assets] Progress: {Processed}/{Total} files ({OverallPercent:F1}%) - Current: {CurrentFile}",
                        progressReport.ProcessedFiles,
                        progressReport.TotalFiles,
                        overallProgressPercent,
                        progressReport.CurrentFile ?? "...");
                }
            });

            bool assetsOk = await assetManager.EnsureAssetsAsync(minecraftVersion, assetProgress, _cts.Token);

            if (_cts.IsCancellationRequested) { Log.Warning("Asset processing cancelled."); return; }

            if (assetsOk)
            {
                Log.Information("All required assets are successfully in place for version {VersionId}.", minecraftVersion.Id);
            }
            else
            {
                Log.Error("Asset download or verification failed for version {VersionId}. Cannot proceed with launch.", minecraftVersion.Id);
                return;
            }

            // --- Step 5: (TODO) Download Libraries ---
            Log.Information("--- Library Download/Verification (Placeholder) ---");
            // Implementation would go into a LibraryManager and called here.
            // bool librariesOk = await libraryManager.EnsureLibrariesAsync(minecraftVersion, progress, _cts.Token);
            // if (!librariesOk) { Log.Error("Library setup failed."); return; }


            // --- Step 6: (TODO) Construct Classpath ---
            Log.Information("--- Classpath Construction (Placeholder) ---");
            // string classpath = libraryManager.BuildClasspath(minecraftVersion, clientJarPath);

            // --- Step 7: (TODO) Construct JVM Arguments ---
            Log.Information("--- JVM Argument Construction (Placeholder) ---");
            // List<string> jvmArgs = argumentBuilder.BuildJvmArguments(minecraftVersion, classpath, nativesDir, authInfo);

            // --- Step 8: (TODO) Construct Game Arguments ---
            Log.Information("--- Game Argument Construction (Placeholder) ---");
            // List<string> gameArgs = argumentBuilder.BuildGameArguments(minecraftVersion, authInfo, windowInfo);

            // --- Step 9: (TODO) Launch Minecraft ---
            Log.Information("--- Launching Minecraft (Placeholder) ---");
            // await gameLauncher.LaunchAsync(javaRuntime.JavaExecutablePath, jvmArgs, gameArgs, workingDirectory);


            Log.Information("Minecraft Launcher (C# Port) simulated run finished.");
        }
        catch (OperationCanceledException) // Catches TaskCanceledException as well
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
            await Log.CloseAndFlushAsync();
        }
    }
}