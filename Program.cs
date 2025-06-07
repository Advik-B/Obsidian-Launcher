// Program.cs
using System;
using System.Collections.Generic; // For List<string>
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
// using ObsidianLauncher.Enums; // If you need direct access to enums here, otherwise they are used within models/services

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
            Environment.ExitCode = 1; // Indicate failure
            return;
        }

        Log.Information("==================================================");
        Log.Information("  Obsidian Minecraft Launcher (C# Port) v0.1");
        Log.Information("==================================================");
        Log.Information("Data directory: {BaseDataPath}", launcherConfig.BaseDataPath);
        Log.Information("Log directory: {LogsDir}", launcherConfig.LogsDir);

        using var httpManager = new HttpManager();
        var javaManager = new JavaManager(launcherConfig, httpManager);
        var assetManager = new AssetManager(launcherConfig, httpManager);
        var libraryManager = new LibraryManager(launcherConfig, httpManager);
        // var argumentBuilder = new ArgumentBuilder(launcherConfig); // Placeholder
        // var gameLauncher = new GameLauncher(launcherConfig);    // Placeholder

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
            // string versionIdToLaunch = "1.21.5"; // Test with your new JSON
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
            var assetProgress = new Progress<AssetDownloadProgress>(report =>
            {
                double overallProgressPercent = (report.TotalFiles > 0)
                    ? (double)report.ProcessedFiles / report.TotalFiles * 100
                    : 0;
                if (report.ProcessedFiles % Math.Max(1, report.TotalFiles / 20) == 0 || report.ProcessedFiles == report.TotalFiles)
                {
                    Log.Information(
                        "[Assets] Progress: {Processed}/{Total} files ({OverallPercent:F1}%) - Current: {CurrentFile}",
                        report.ProcessedFiles, report.TotalFiles, overallProgressPercent, report.CurrentFile ?? "...");
                }
            });

            bool assetsOk = await assetManager.EnsureAssetsAsync(minecraftVersion, assetProgress, _cts.Token);

            if (_cts.IsCancellationRequested) { Log.Warning("Asset processing cancelled."); return; }

            if (!assetsOk)
            {
                Log.Error("Asset download or verification failed for version {VersionId}. Cannot proceed with launch.", minecraftVersion.Id);
                return;
            }
            Log.Information("All required assets are successfully in place for version {VersionId}.", minecraftVersion.Id);


            // --- Step 5: Download Libraries & Extract Natives ---
            Log.Information("--- Ensuring Libraries for Minecraft {VersionId} ---", minecraftVersion.Id);
            string versionSpecificDir = Path.Combine(launcherConfig.VersionsDir, minecraftVersion.Id);
            Directory.CreateDirectory(versionSpecificDir); // Ensure version-specific directory exists
            string nativesDirectory = Path.Combine(versionSpecificDir, $"{minecraftVersion.Id}-natives");
            Log.Information("Natives will be extracted to: {NativesDirectory}", nativesDirectory);


            var libraryProgress = new Progress<LibraryProcessingProgress>(report =>
            {
                // More detailed or less frequent logging for libraries
                if (report.Status.Contains("failed", StringComparison.OrdinalIgnoreCase) || report.Status.Contains("Skipped") || report.ProcessedLibraries % Math.Max(1, report.TotalLibraries / 10) == 0 || report.ProcessedLibraries == report.TotalLibraries)
                {
                    Log.Information("[Libs] {Processed}/{Total} - Status: {Status} - Lib: {LibraryName}",
                        report.ProcessedLibraries, report.TotalLibraries, report.Status, report.CurrentLibraryName);
                }
                else
                {
                    Log.Verbose("[Libs] {Processed}/{Total} - Status: {Status} - Lib: {LibraryName}",
                        report.ProcessedLibraries, report.TotalLibraries, report.Status, report.CurrentLibraryName);
                }
            });

            List<string> libraryClasspathEntries = await libraryManager.EnsureLibrariesAsync(minecraftVersion, nativesDirectory, libraryProgress, _cts.Token);

            if (_cts.IsCancellationRequested) { Log.Warning("Library processing cancelled."); return; }

            if (libraryClasspathEntries == null) // EnsureLibrariesAsync returns null on critical failure
            {
                Log.Error("Failed to process one or more libraries for version {VersionId}. Cannot proceed.", minecraftVersion.Id);
                return;
            }
            Log.Information("All applicable libraries processed successfully for version {VersionId}. Library classpath entries: {Count}",
                minecraftVersion.Id, libraryClasspathEntries.Count);


            // --- Step 5.5: Download Client JAR ---
            Log.Information("--- Ensuring Client JAR for Minecraft {VersionId} ---", minecraftVersion.Id);
            string clientJarPath = Path.Combine(versionSpecificDir, $"{minecraftVersion.Id}.jar");
            bool clientJarOk = false;
            if (minecraftVersion.Downloads.TryGetValue("client", out DownloadDetails clientDownloadDetails))
            {
                clientJarOk = await assetManager.DownloadAndVerifyFileAsync( // Reusing AssetManager's helper
                    clientDownloadDetails.Url,
                    clientJarPath,
                    clientDownloadDetails.Sha1,
                    $"Client JAR for {minecraftVersion.Id}",
                    _cts.Token,
                    clientDownloadDetails.Size);
            }
            else
            {
                Log.Error("No client JAR download information found for version {VersionId}.", minecraftVersion.Id);
            }

            if (_cts.IsCancellationRequested) { Log.Warning("Client JAR download cancelled."); return; }

            if (!clientJarOk)
            {
                Log.Error("Failed to download or verify client JAR for version {VersionId}. Cannot proceed.", minecraftVersion.Id);
                return;
            }
            Log.Information("Client JAR for version {VersionId} is ready at {ClientJarPath}", minecraftVersion.Id, clientJarPath);


            // --- Step 6: Construct Classpath ---
            Log.Information("--- Classpath Construction ---");
            List<string> finalClasspathEntries = new List<string>();
            if (File.Exists(clientJarPath))
            {
                finalClasspathEntries.Add(Path.GetFullPath(clientJarPath));
            }
            else
            {
                Log.Error("CRITICAL: Client JAR {ClientJarPath} not found even after download attempt. Classpath will be incomplete.", clientJarPath);
                // This should ideally not happen if clientJarOk was true.
            }
            finalClasspathEntries.AddRange(libraryClasspathEntries); // These are already full paths

            string classpathString = string.Join(Path.PathSeparator.ToString(), finalClasspathEntries);
            Log.Information("Final Classpath contains {Count} entries.", finalClasspathEntries.Count);
            Log.Verbose("Classpath Preview (first 200 chars): {ClasspathPreview}",
                classpathString.Length > 200 ? classpathString.Substring(0, 200) + "..." : classpathString);
            // For full classpath in verbose/debug: Log.Verbose("Full Classpath: {ClasspathString}", classpathString);


            // --- Step 7: (TODO) Construct JVM Arguments ---
            Log.Information("--- JVM Argument Construction (Placeholder) ---");
            // List<string> jvmArgs = argumentBuilder.BuildJvmArguments(minecraftVersion, classpathString, nativesDirectory, authInfo /*, etc */);

            // --- Step 8: (TODO) Construct Game Arguments ---
            Log.Information("--- Game Argument Construction (Placeholder) ---");
            // List<string> gameArgs = argumentBuilder.BuildGameArguments(minecraftVersion, authInfo, windowInfo /*, etc */);

            // --- Step 9: (TODO) Launch Minecraft ---
            Log.Information("--- Launching Minecraft (Placeholder) ---");
            // await gameLauncher.LaunchAsync(javaRuntime.JavaExecutablePath, jvmArgs, gameArgs, versionSpecificDir /* or game_directory */);


            Log.Information("Minecraft Launcher (C# Port) setup phase finished for version {VersionId}.", minecraftVersion.Id);
        }
        catch (OperationCanceledException)
        {
            Log.Warning("A long-running operation was cancelled by the user or a timeout.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "An unhandled exception occurred in the main application flow.");
            Environment.ExitCode = 1;
        }
        finally
        {
            Log.Information("Shutting down logger...");
            await Log.CloseAndFlushAsync();
            if (Environment.ExitCode != 0)
            {
                 Console.WriteLine("Launcher exited with errors. Check logs for details.");
            }
        }
    }
}