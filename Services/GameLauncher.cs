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
using System;
using System.Collections.Generic;
using System.Diagnostics; // For Process and ProcessStartInfo
using System.IO;
using System.Linq;
using System.Text; // For StringBuilder
using System.Threading;
using System.Threading.Tasks;
using Serilog;
// Assuming LauncherConfig is in ObsidianLauncher namespace
using ObsidianLauncher;

namespace ObsidianLauncher.Services
{
    public class GameLauncher
    {
        private readonly LauncherConfig _config;
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
        /// <param name="cancellationToken">Optional token to allow for early termination signal.</param>
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

            var argumentsBuilder = new StringBuilder();
            bool firstElementAddedToBuilder = false;

            // Local action to handle argument appending and quoting
            Action<string> appendQuotedArgument = (argVal) =>
            {
                if (string.IsNullOrEmpty(argVal)) // Skip null or truly empty strings from lists
                {
                    // For an explicitly empty argument passed by Minecraft's spec (rare), it might be ""
                    // But generally, we filter these out from the lists *before* calling this.
                    // If an argument *must* be an empty quoted string, the list should contain `""`.
                    // Here, we assume if argVal is null/empty, it was an unintentional empty element in the list.
                    _logger.Verbose("Skipping append of null or empty argument string to command line.");
                    return;
                }

                if (firstElementAddedToBuilder)
                {
                    argumentsBuilder.Append(' ');
                }

                // Simple quoting: if it contains a space or is already quoted (e.g. from placeholder replacement),
                // or if it's an empty string that needs to be represented as "".
                // More robust command-line quoting can be very complex depending on the OS and shell.
                // For ProcessStartInfo, simple quoting for spaces is usually sufficient.
                // If an argument is already correctly quoted (e.g. from ReplacePlaceholders), don't double-quote.
                bool needsQuotes = argVal.Contains(' ') && !(argVal.StartsWith("\"") && argVal.EndsWith("\""));
                
                if (needsQuotes)
                {
                    argumentsBuilder.Append('"');
                }
                // Escape internal quotes if we are adding quotes.
                // Java CLI is generally tolerant of `"` inside an argument if the whole thing is quoted.
                // A common way to pass a quote inside a quoted string is to double it `""` or backslash it `\"`.
                // ProcessStartInfo might handle some of this, but explicit escaping is safer.
                // For simplicity and common Java usage, we'll just ensure the whole arg is quoted if it has spaces.
                // If an argument *value* itself needs to contain quotes, the `ReplacePlaceholders` should handle that.
                argumentsBuilder.Append(argVal);

                if (needsQuotes)
                {
                    argumentsBuilder.Append('"');
                }
                firstElementAddedToBuilder = true;
            };

            // Add JVM arguments
            if (jvmArguments != null)
            {
                foreach (var arg in jvmArguments)
                {
                    // Individual JVM arguments from the list should already be complete strings,
                    // possibly already quoted by ReplacePlaceholders if they were path variables.
                    appendQuotedArgument(arg);
                }
            }

            // Add main class
            appendQuotedArgument(mainClass);

            // Add game arguments
            if (gameArguments != null)
            {
                foreach (var arg in gameArguments)
                {
                    // Individual game arguments from the list.
                    appendQuotedArgument(arg);
                }
            }

            string finalArguments = argumentsBuilder.ToString();

            _logger.Information("Attempting to launch Minecraft...");
            _logger.Information("  Java Executable: {JavaPath}", javaExecutablePath);
            _logger.Information("  Working Directory: {WorkDir}", workingDirectory);

            // Log arguments carefully, they can be very long
            if (finalArguments.Length < 1024) // Log full if reasonably short
                _logger.Information("  Full JVM, MainClass & Game Arguments: {Arguments}", finalArguments);
            else
                _logger.Information("  Full JVM, MainClass & Game Arguments (first 1024 chars): {Arguments}...", finalArguments.Substring(0,1024));
            _logger.Verbose("  Full JVM, MainClass & Game Arguments (Complete): {Arguments}", finalArguments);


            var processStartInfo = new ProcessStartInfo
            {
                FileName = javaExecutablePath,
                Arguments = finalArguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = Path.GetFileName(javaExecutablePath).Equals("javaw.exe", StringComparison.OrdinalIgnoreCase)
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.EnableRaisingEvents = true;

            // Use TaskCompletionSource to properly await async event handlers if needed,
            // or simply log directly. For console output, direct logging is fine.
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    _logger.Information("[Minecraft STDOUT] {Data}", e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    _logger.Error("[Minecraft STDERR] {Data}", e.Data);
                }
            };

            try
            {
                _logger.Information("Starting Minecraft process (ID will be assigned by OS)...");
                if (!process.Start())
                {
                    _logger.Error("Failed to start Minecraft process. Process.Start() returned false.");
                    return -1; // Indicate failure to start
                }

                _logger.Information("Minecraft process successfully started with ID: {ProcessId}. Attaching output readers.", process.Id);

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Asynchronously wait for the process to exit or cancellation.
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                // Ensure all output is flushed after process exits but before we declare it finished
                // This might not be strictly necessary if reading is complete, but can help catch trailing messages.
                // However, once exited, BeginOutputReadLine/BeginErrorReadLine might have already finished.
                // The most reliable way is that the events themselves handle all data.

                _logger.Information("Minecraft process (ID: {ProcessId}) has exited with code: {ExitCode}", process.Id, process.ExitCode);
                return process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Launch operation was cancelled. Minecraft process (ID: {ProcessId}) may still be running or was terminated.", process.Id);
                if (!process.HasExited)
                {
                    _logger.Information("Attempting to kill Minecraft process (ID: {ProcessId}) due to cancellation.", process.Id);
                    try
                    {
                        process.Kill(entireProcessTree: true); // Kill the process and any child processes it might have spawned
                        _logger.Information("Minecraft process (ID: {ProcessId}) killed successfully due to cancellation.", process.Id);
                    }
                    catch (InvalidOperationException ioe) when (ioe.Message.Contains("process has already exited"))
                    {
                         _logger.Information("Minecraft process (ID: {ProcessId}) had already exited when kill was attempted after cancellation.", process.Id);
                    }
                    catch (Exception killEx)
                    {
                        _logger.Error(killEx, "Exception while trying to kill Minecraft process (ID: {ProcessId}) after cancellation.", process.Id);
                    }
                }
                return -100; // Specific exit code for cancellation
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "An exception occurred while launching or monitoring the Minecraft process (ID: {ProcessId}).", process.Id);
                if (!process.HasExited)
                {
                    try { process.Kill(true); } catch { /* Best effort to kill */ }
                }
                return -1; // General error code
            }
            finally
            {
                // It's good practice to dispose of the process object,
                // though `using var process = ...` already handles this.
                // If not using `using`, then `process.Dispose()` would be here.
            }
        }
    }

    /// <summary>
    /// Extension method to properly quote arguments for command line usage.
    /// </summary>
    // Services/GameLauncher.cs (or wherever StringBuilderExtensions is)
    public static class StringBuilderExtensions
    {
        public static StringBuilder AppendArgument(this StringBuilder sb, string argument)
        {
            // If the argument is null or purely whitespace, and it's not the first thing
            // we are appending (meaning sb is not empty), we might still want a space
            // to separate from a previous valid argument, followed by empty quotes.
            // However, if it's the first argument and it's null/empty, we should append nothing.

            if (string.IsNullOrWhiteSpace(argument))
            {
                if (sb.Length > 0) // If there's already content, add a space then empty quotes
                {
                    sb.Append(' ');
                }
                sb.Append("\"\""); // Represent empty argument as quoted empty string
                return sb;
            }

            // If sb is not empty, means we are appending another argument, so add a space first.
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            // Quoting logic for non-empty arguments
            if (argument.Contains(' ') || argument.Contains('"'))
            {
                // Basic escaping: double up existing quotes
                string escapedArgument = argument.Replace("\"", "\\\""); // For " inside "
                // A more robust solution for Windows might involve more complex escaping
                // or relying on how .NET's ProcessStartInfo handles array of args if that was an option.
                // For now, this is a common approach.
                sb.Append('"').Append(escapedArgument).Append('"');
            }
            else
            {
                sb.Append(argument);
            }
            return sb;
        }
    }
}