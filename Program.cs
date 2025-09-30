using System;
using Avalonia;
using Avalonia.ReactiveUI;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher;

class Program
{
    // This method will be called when the application starts.
    public static void Main(string[] args)
    {
        try
        {
            // Initialize logging first
            var launcherConfig = new LauncherConfig();
            LoggerSetup.Initialize(launcherConfig);

            Log.Information("==================================================");
            Log.Information("  Obsidian Launcher {Version} (GUI)", $"v{LauncherConfig.VERSION}");
            Log.Information("==================================================");
            Log.Information("Data directory: {BaseDataPath}", launcherConfig.BaseDataPath);

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Critical startup error: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            Environment.ExitCode = 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}