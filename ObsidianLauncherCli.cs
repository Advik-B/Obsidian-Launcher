using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        var libraryManager = new LibraryManager(launcherConfig, httpManager, assetManager);
        var instanceManager = new InstanceManager(launcherConfig, assetManager, libraryManager, httpManager);
        var argumentBuilder = new ArgumentBuilder(launcherConfig);
        var gameLauncher = new GameLauncher(launcherConfig);

        try
        {
            // Default values
            var mcVersion = "1.20.4";
            var instanceName = "Fabric 1.20.4";
            var loaderName = "fabric";
            var loaderVersion = "0.15.7";
            string? cliPlayerNameFromArg = null;

            // Basic command-line parsing
            if (args.Length > 0) mcVersion = args[0];
            if (args.Length > 1) instanceName = args[1];
            if (args.Length > 2) loaderName = args[2];
            if (args.Length > 3) loaderVersion = args[3];
            if (args.Length > 4) cliPlayerNameFromArg = args[4];

            var sessionPlayerName = cliPlayerNameFromArg ?? $"Player{Random.Shared.Next(100, 999)}";
            argumentBuilder.SetOfflinePlayerName(sessionPlayerName);

            var components = new List<Component>
            {
                new() { Uid = "net.minecraft", Version = mcVersion, IsImportant = true }
            };

            if (!string.IsNullOrEmpty(loaderName) && !string.IsNullOrEmpty(loaderVersion))
            {
                string? loaderUid = loaderName.ToLower() switch
                {
                    "fabric" => "net.fabricmc.fabric-loader",
                    // Add other loaders here
                    _ => null
                };

                if (loaderUid != null)
                {
                    components.Add(new Component { Uid = loaderUid, Version = loaderVersion });
                    Log.Information("Configuring instance with {LoaderName} {LoaderVersion}", loaderName, loaderVersion);
                }
            }

            var assetProgress = new Progress<AssetDownloadProgress>(report =>
            {
                if (report.ProcessedFiles % Math.Max(1, report.TotalFiles / 20) == 0 || report.ProcessedFiles == report.TotalFiles)
                    Log.Information("[Assets] Progress: {Processed}/{Total} files ({OverallPercent:F1}%) - Current: {CurrentFile}",
                        report.ProcessedFiles, report.TotalFiles, report.TotalFiles > 0 ? (double)report.ProcessedFiles / report.TotalFiles * 100 : 0, report.CurrentFile ?? "...");
            });
            var libraryProgress = new Progress<LibraryProcessingProgress>(report =>
            {
                if (report.Status?.Contains("failed", StringComparison.OrdinalIgnoreCase) == true || report.Status?.Contains("Skipped") == true || report.ProcessedLibraries % Math.Max(1, report.TotalLibraries / 10) == 0 || report.ProcessedLibraries == report.TotalLibraries)
                    Log.Information("[Libs] {Processed}/{Total} - Status: {Status} - Lib: {LibraryName}", report.ProcessedLibraries, report.TotalLibraries, report.Status, report.CurrentLibraryName ?? "");
                else
                    Log.Verbose("[Libs] {Processed}/{Total} - Status: {Status} - Lib: {LibraryName}", report.ProcessedLibraries, report.TotalLibraries, report.Status, report.CurrentLibraryName ?? "");
            });

            var (currentInstance, clientJarPath, libraryClasspathEntries) = await instanceManager.GetOrCreateInstanceAsync(
                instanceName,
                components,
                assetProgress,
                libraryProgress,
                _cts.Token
            );

            if (currentInstance == null || string.IsNullOrEmpty(clientJarPath) || libraryClasspathEntries == null)
            {
                Log.Fatal("Failed to get, create, or sync instance '{InstanceName}'. Cannot proceed.", instanceName);
                return;
            }

            Log.Information("Instance ready: '{InstanceName}' (ID: {InstanceId}) at {InstancePath}", currentInstance.Name, currentInstance.Id, currentInstance.InstancePath);

            // Re-build the final launch profile to pass to JavaManager and ArgumentBuilder
            var launchProfile = await instanceManager.BuildLaunchProfileAsync(currentInstance.Components, _cts.Token);
            if (launchProfile == null)
            {
                Log.Fatal("Failed to build final launch profile for instance '{InstanceName}'. Cannot launch.", currentInstance.Name);
                return;
            }

            Log.Information("--- Ensuring Java Runtime for Minecraft {VersionId} ---", launchProfile.Id);
            var javaRuntime = await javaManager.EnsureJavaForMinecraftVersionAsync(launchProfile, _cts.Token);

            if (javaRuntime == null)
            {
                Log.Error("Failed to obtain a suitable Java runtime for instance '{InstanceName}'. Cannot proceed.", currentInstance.Name);
                return;
            }
            Log.Information("Java Runtime Ensured: {JavaExecutablePath}", javaRuntime.JavaExecutablePath);

            var classpathString = argumentBuilder.BuildClasspath(clientJarPath!, libraryClasspathEntries!);
            var jvmArgs = argumentBuilder.BuildJvmArguments(launchProfile, classpathString, currentInstance.NativesPath, javaRuntime, currentInstance.InstancePath);
            var gameArgs = argumentBuilder.BuildGameArguments(launchProfile, currentInstance.InstancePath);

            Log.Information("--- Launching instance '{InstanceName}' (Player: {PlayerName}) ---", currentInstance.Name, sessionPlayerName);
            var gameWorkingDirectory = Path.GetFullPath(currentInstance.GameDataPath);

            var sessionStartTime = DateTime.UtcNow;
            var exitCode = await gameLauncher.LaunchAsync(javaRuntime.JavaExecutablePath, jvmArgs, launchProfile.MainClass, gameArgs, gameWorkingDirectory, cancellationToken: _cts.Token);
            var sessionDuration = DateTime.UtcNow - sessionStartTime;
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
                    Log.Warning("Minecraft exited with a non-zero exit code ({ExitCode}), check instance logs: {InstanceLogPath}",
                        exitCode, Path.Combine(currentInstance.GameDataPath, "logs"));
            }

            Log.Information("Obsidian Launcher has completed its operation for instance '{InstanceName}'.", currentInstance.Name);
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