// Services/GameLauncher.cs
using System;
using System.Collections.Generic;
using System.Diagnostics; // For Process and ProcessStartInfo
using System.IO;
using System.Linq;
using System.Text; // For StringBuilder
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace ObsidianLauncher.Services
{
    public class GameLauncher
    {
        private readonly LauncherConfig _config; // May not be strictly needed if workingDirectory is passed explicitly
        private readonly ILogger _logger;

        public GameLauncher(LauncherConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = Log.ForContext<GameLauncher>();
            _logger.Verbose("GameLauncher initialized.");
        }

        /// <summary>
        /// Launches the Minecraft game process.
        /// </summary>
        /// <param name="javaExecutablePath">Full path to the java/javaw executable.</param>
        /// <param name="jvmArguments">List of arguments for the JVM (e.g., -Xmx, -Djava.library.path).</param>
        /// <param name="mainClass">The main class to execute (e.g., net.minecraft.client.main.Main).</param>
        /// <param name="gameArguments">List of arguments for the Minecraft game itself.</param>
        /// <param name="workingDirectory">The working directory for the Minecraft process.</param>
        /// <param name="cancellationToken">Optional token to allow for early termination signal (though Process.Kill is more direct).</param>
        /// <returns>A Task representing the asynchronous operation. The task completes when the Minecraft process exits. Returns the exit code of the process.</returns>
        public async Task<int> LaunchAsync(
            string javaExecutablePath,
            List<string> jvmArguments,
            string mainClass,
            List<string> gameArguments,
            string workingDirectory,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(javaExecutablePath) || !File.Exists(javaExecutablePath))
            {
                _logger.Error("Java executable path is invalid or file does not exist: {JavaPath}", javaExecutablePath);
                throw new FileNotFoundException("Java executable not found.", javaExecutablePath);
            }
            if (string.IsNullOrWhiteSpace(mainClass))
            {
                _logger.Error("Main class for Minecraft is not specified.");
                throw new ArgumentException("Main class cannot be empty.", nameof(mainClass));
            }
            if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
            {
                _logger.Error("Working directory is invalid or does not exist: {WorkingDirectory}", workingDirectory);
                throw new DirectoryNotFoundException($"Working directory not found: {workingDirectory}");
            }

            // Construct the full argument string
            var argumentsBuilder = new StringBuilder();

            // Add JVM arguments
            if (jvmArguments != null)
            {
                foreach (var arg in jvmArguments)
                {
                    argumentsBuilder.AppendArgument(arg); // Custom extension method for proper quoting
                }
            }

            // Add main class
            argumentsBuilder.AppendArgument(mainClass);

            // Add game arguments
            if (gameArguments != null)
            {
                foreach (var arg in gameArguments)
                {
                    argumentsBuilder.AppendArgument(arg);
                }
            }

            string finalArguments = argumentsBuilder.ToString().Trim();

            _logger.Information("Attempting to launch Minecraft...");
            _logger.Information("  Java Executable: {JavaPath}", javaExecutablePath);
            _logger.Information("  Working Directory: {WorkDir}", workingDirectory);
            _logger.Verbose("  Full JVM & Game Arguments: {Arguments}", finalArguments);


            var processStartInfo = new ProcessStartInfo
            {
                FileName = javaExecutablePath,
                Arguments = finalArguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,        // Important for redirecting IO
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false          // Set to true if you want to hide the Java console window (for javaw.exe this is often default)
                                                // For debugging, false is useful to see any direct Java console output.
            };

            // For Windows, if using javaw.exe, CreateNoWindow = true is typical for a "silent" launch.
            // If using java.exe, CreateNoWindow = false will show a console.
            // On Linux/macOS, this flag has less impact on a visible console for GUI apps.
            if (Path.GetFileName(javaExecutablePath).Equals("javaw.exe", StringComparison.OrdinalIgnoreCase))
            {
                processStartInfo.CreateNoWindow = true;
            }


            using var process = new Process { StartInfo = processStartInfo };

            // Enable raising events before setting up handlers
            process.EnableRaisingEvents = true;

            // Data received handlers must be set before starting the process
            // and before enabling event raising if you're relying on Exited event.
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    // Log Minecraft's standard output
                    // You might want different log levels or formatting here
                    _logger.Information("[Minecraft STDOUT] {Data}", e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    // Log Minecraft's standard error
                    _logger.Error("[Minecraft STDERR] {Data}", e.Data);
                }
            };

            try
            {
                _logger.Information("Starting Minecraft process...");
                if (!process.Start())
                {
                    _logger.Error("Failed to start Minecraft process.");
                    return -1; // Or throw an exception
                }

                _logger.Information("Minecraft process started with ID: {ProcessId}", process.Id);

                // Begin asynchronously reading the output streams.
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Asynchronously wait for the process to exit.
                // You can use the CancellationToken to attempt to kill the process if cancellation is requested.
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                _logger.Information("Minecraft process exited with code: {ExitCode}", process.ExitCode);
                return process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Launch operation was cancelled. Attempting to kill Minecraft process if running.");
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true); // Kill the process and any child processes
                        _logger.Information("Minecraft process killed due to cancellation.");
                    }
                    catch (Exception killEx)
                    {
                        _logger.Error(killEx, "Exception while trying to kill Minecraft process after cancellation.");
                    }
                }
                return -100; // Specific exit code for cancellation
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "An exception occurred while launching or monitoring the Minecraft process.");
                if (!process.HasExited)
                {
                    try { process.Kill(true); } catch { /* Ignore */ }
                }
                return -1; // Or rethrow
            }
            finally
            {
                // Ensure output/error streams are cancelled if process is still running
                // (e.g. if WaitForExitAsync was cancelled but process didn't die immediately)
                if (!process.HasExited)
                {
                    try { process.CancelOutputRead(); } catch { /* Ignore */ }
                    try { process.CancelErrorRead(); } catch { /* Ignore */ }
                }
            }
        }
    }

    /// <summary>
    /// Extension method to properly quote arguments for command line usage.
    /// </summary>
    public static class StringBuilderExtensions
    {
        public static StringBuilder AppendArgument(this StringBuilder sb, string argument)
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            if (string.IsNullOrEmpty(argument))
            {
                sb.Append("\"\""); // Empty argument
                return sb;
            }

            // If argument contains spaces, quotes, or needs escaping, quote it.
            // Simple quoting: if it contains a space, wrap in quotes.
            // More robust quoting would handle existing quotes within the argument.
            if (argument.Contains(' ') || argument.Contains('"'))
            {
                // Escape existing quotes by doubling them, then wrap the whole thing in quotes.
                // This is a common convention, but behavior can vary by how arguments are parsed.
                // For java.exe, simply wrapping with quotes is usually sufficient if internal quotes are not an issue.
                // If internal quotes are present, they might need to be escaped with backslashes for some parsers.
                // Java itself usually handles arguments passed via ProcessStartInfo correctly without complex shell escaping.
                sb.Append('"').Append(argument.Replace("\"", "\\\"")).Append('"'); // Basic escaping for internal quotes
            }
            else
            {
                sb.Append(argument);
            }
            return sb;
        }
    }
}