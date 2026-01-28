using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace ObsidianLauncher.Utils; // Correct namespace based on folder structure

public static class LoggerSetup
{
    private const string LogTemplate =
        "{Timestamp:hh:mm:ss.fff tt} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";


    /// <summary>
    ///     Initializes the global Serilog logger with console and file sinks.
    /// </summary>
    /// <param name="config">The launcher configuration containing the logs directory path.</param>
    /// <param name="consoleLevel">The minimum log level for the console sink.</param>
    /// <param name="fileLevel">The minimum log level for the file sink.</param>
    public static void Initialize(
        LauncherConfig config, // Fully qualified type
        LogEventLevel consoleLevel = LogEventLevel.Information,
        LogEventLevel fileLevel = LogEventLevel.Verbose) // 'Verbose' in Serilog is similar to 'Trace' in spdlog
    {
        var logFilePath = Path.Combine(config.LogsDir, "launcher-.log"); // Serilog adds date to filename for rolling

        try
        {
            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Verbose() // Set the global minimum level; sinks can override with a higher minimum.
                .Enrich.FromLogContext() // Essential for adding SourceContext or other enrichers
                .WriteTo.Console(
                    consoleLevel,
                    LogTemplate
                );

            if (!string.IsNullOrEmpty(config.LogsDir)) // Only add file sink if LogsDir is configured
                loggerConfiguration.WriteTo.File(
                    logFilePath,
                    fileLevel,
                    rollingInterval: RollingInterval.Day, // New log file daily
                    rollOnFileSizeLimit: true, // Start new file if current one gets too big
                    fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB limit per file
                    retainedFileCountLimit: 7, // Keep the last 7 log files
                    outputTemplate: LogTemplate,
                    shared: false // false = exclusive lock, true = shared (can be slower)
                );
            else
                // Log a warning if file logging is skipped due to missing config
                // This needs to be done carefully if the main logger isn't set yet.
                Console.WriteLine(
                    "Warning: LogsDir is not configured in LauncherConfig. File logging will be skipped.");


            Log.Logger = loggerConfiguration.CreateLogger();

            Log.Information(
                "Logger initialized. Console Level: {ConsoleLogLevel}, File Level: {FileLogLevel} (Path: {LogFile})",
                consoleLevel,
                fileLevel,
                Path.GetFullPath(logFilePath));
        }
        catch (Exception ex)
        {
            // Fallback logger if initialization fails
            Console.Error.WriteLine($"Log initialization failed: {ex.Message}");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .WriteTo.Console()
                .CreateLogger();
            Log.Error(ex, "LOGGER INITIALIZATION FAILED. Using basic console fallback.");
        }
    }

    /// <summary>
    ///     Sets the logging level for a specific logger context.
    ///     Note: Serilog's dynamic level control is more complex than spdlog's simple SetLevel.
    ///     This is a simplified approach. For true dynamic control, you'd use Serilog.Settings.Configuration
    ///     or a LoggingLevelSwitch. This example won't dynamically change levels of already created loggers
    ///     without reconfiguring the global Log.Logger or using LoggingLevelSwitch.
    ///     For this project, it's often sufficient to set the desired level when creating the logger:
    ///     Log.ForContext&lt;MyClass&gt;().IsEnabled(LogEventLevel.Verbose)
    ///     Or filter at the sink level.
    /// </summary>
    /// <param name="loggerName">This parameter is not directly used in simple Serilog setup without level switches.</param>
    /// <param name="level">The desired minimum level.</param>
    public static void SetLevel(string loggerName, LogEventLevel level)
    {
        // Serilog doesn't have a direct equivalent to spdlog's per-logger SetLevel
        // after the main logger is configured, unless using LoggingLevelSwitch.
        // Typically, you filter at the sink level or when creating a contextual logger.
        // This function is more of a placeholder or would require a more advanced Serilog setup.
        Log.Warning(
            "Serilog's SetLevel for a specific context ('{LoggerName}') after initialization is not directly supported in this basic setup. Minimum levels are generally controlled globally or at the sink level. Consider using LoggingLevelSwitch for dynamic control.",
            loggerName);

        // If you wanted to change the *global* minimum level:
        // This is generally not what you want for a specific logger.
        // LoggerConfiguration newConfig = ... recreate with new MinimumLevel(level) ...
        // Log.Logger = newConfig.CreateLogger();
    }
}