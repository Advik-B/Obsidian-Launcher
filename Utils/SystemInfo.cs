// Utils/SystemInfo.cs

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management;
using Serilog;

namespace ObsidianLauncher.Utils;

/// <summary>
///     Utility class for gathering system information (RAM, CPU, GPU, etc.).
/// </summary>
public static class SystemInfo
{
    private static readonly ILogger _logger = Log.ForContext(typeof(SystemInfo));

    /// <summary>
    ///     Gets the total amount of physical RAM in megabytes.
    /// </summary>
    /// <returns>Total RAM in MB, or -1 if unable to determine.</returns>
    public static long GetTotalMemoryMB()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsTotalMemoryMB();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxTotalMemoryMB();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetMacOSTotalMemoryMB();
            }

            _logger.Warning("Unsupported OS for memory detection");
            return -1;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get total memory");
            return -1;
        }
    }

    /// <summary>
    ///     Gets the available (free) RAM in megabytes.
    /// </summary>
    /// <returns>Available RAM in MB, or -1 if unable to determine.</returns>
    public static long GetAvailableMemoryMB()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsAvailableMemoryMB();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxAvailableMemoryMB();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetMacOSAvailableMemoryMB();
            }

            _logger.Warning("Unsupported OS for available memory detection");
            return -1;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get available memory");
            return -1;
        }
    }

    /// <summary>
    ///     Gets the CPU name/model.
    /// </summary>
    /// <returns>CPU name, or "Unknown" if unable to determine.</returns>
    public static string GetCPUName()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsCPUName();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxCPUName();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetMacOSCPUName();
            }

            return "Unknown CPU";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get CPU name");
            return "Unknown CPU";
        }
    }

    /// <summary>
    ///     Gets the number of logical CPU cores.
    /// </summary>
    /// <returns>Number of logical cores.</returns>
    public static int GetCPUCoreCount()
    {
        return Environment.ProcessorCount;
    }

    /// <summary>
    ///     Gets GPU information (primary graphics card).
    /// </summary>
    /// <returns>GPU name, or "Unknown" if unable to determine.</returns>
    public static string GetGPUName()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsGPUName();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxGPUName();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetMacOSGPUName();
            }

            return "Unknown GPU";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get GPU name");
            return "Unknown GPU";
        }
    }

    /// <summary>
    ///     Formats a byte size into a human-readable string (e.g., "1.5 GB").
    /// </summary>
    /// <param name="bytes">Size in bytes.</param>
    /// <returns>Formatted string.</returns>
    public static string FormatByteSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:0.##} {suffixes[suffixIndex]}";
    }

    /// <summary>
    ///     Gets disk space information for a given path.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>Tuple of (available space in bytes, total space in bytes), or (-1, -1) on error.</returns>
    public static (long Available, long Total) GetDiskSpace(string path)
    {
        try
        {
            var driveInfo = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(path));
            return (driveInfo.AvailableFreeSpace, driveInfo.TotalSize);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get disk space for path: {Path}", path);
            return (-1, -1);
        }
    }

    // Windows-specific implementations
    private static long GetWindowsTotalMemoryMB()
    {
        using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
        foreach (var obj in searcher.Get())
        {
            var totalBytes = Convert.ToInt64(obj["TotalPhysicalMemory"]);
            return totalBytes / (1024 * 1024);
        }
        return -1;
    }

    private static long GetWindowsAvailableMemoryMB()
    {
        using var searcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem");
        foreach (var obj in searcher.Get())
        {
            var freeKB = Convert.ToInt64(obj["FreePhysicalMemory"]);
            return freeKB / 1024;
        }
        return -1;
    }

    private static string GetWindowsCPUName()
    {
        using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
        foreach (var obj in searcher.Get())
        {
            return obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
        }
        return "Unknown CPU";
    }

    private static string GetWindowsGPUName()
    {
        using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
        foreach (var obj in searcher.Get())
        {
            return obj["Name"]?.ToString()?.Trim() ?? "Unknown GPU";
        }
        return "Unknown GPU";
    }

    // Linux-specific implementations
    private static long GetLinuxTotalMemoryMB()
    {
        var memInfo = System.IO.File.ReadAllText("/proc/meminfo");
        var match = System.Text.RegularExpressions.Regex.Match(memInfo, @"MemTotal:\s+(\d+)\s+kB");
        if (match.Success)
        {
            var totalKB = long.Parse(match.Groups[1].Value);
            return totalKB / 1024;
        }
        return -1;
    }

    private static long GetLinuxAvailableMemoryMB()
    {
        var memInfo = System.IO.File.ReadAllText("/proc/meminfo");
        var match = System.Text.RegularExpressions.Regex.Match(memInfo, @"MemAvailable:\s+(\d+)\s+kB");
        if (match.Success)
        {
            var availableKB = long.Parse(match.Groups[1].Value);
            return availableKB / 1024;
        }
        return -1;
    }

    private static string GetLinuxCPUName()
    {
        var cpuInfo = System.IO.File.ReadAllText("/proc/cpuinfo");
        var match = System.Text.RegularExpressions.Regex.Match(cpuInfo, @"model name\s+:\s+(.+)");
        return match.Success ? match.Groups[1].Value.Trim() : "Unknown CPU";
    }

    private static string GetLinuxGPUName()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "lspci",
                    Arguments = "",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var match = System.Text.RegularExpressions.Regex.Match(output, @"VGA compatible controller:\s+(.+)");
            return match.Success ? match.Groups[1].Value.Trim() : "Unknown GPU";
        }
        catch
        {
            return "Unknown GPU";
        }
    }

    // macOS-specific implementations
    private static long GetMacOSTotalMemoryMB()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sysctl",
                    Arguments = "-n hw.memsize",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (long.TryParse(output, out var totalBytes))
            {
                return totalBytes / (1024 * 1024);
            }
        }
        catch { }
        return -1;
    }

    private static long GetMacOSAvailableMemoryMB()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "vm_stat",
                    Arguments = "",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var match = System.Text.RegularExpressions.Regex.Match(output, @"Pages free:\s+(\d+)");
            if (match.Success && long.TryParse(match.Groups[1].Value, out var freePages))
            {
                // macOS page size is typically 4096 bytes
                return (freePages * 4096) / (1024 * 1024);
            }
        }
        catch { }
        return -1;
    }

    private static string GetMacOSCPUName()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sysctl",
                    Arguments = "-n machdep.cpu.brand_string",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return !string.IsNullOrWhiteSpace(output) ? output : "Unknown CPU";
        }
        catch
        {
            return "Unknown CPU";
        }
    }

    private static string GetMacOSGPUName()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "system_profiler",
                    Arguments = "SPDisplaysDataType",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var match = System.Text.RegularExpressions.Regex.Match(output, @"Chipset Model:\s+(.+)");
            return match.Success ? match.Groups[1].Value.Trim() : "Unknown GPU";
        }
        catch
        {
            return "Unknown GPU";
        }
    }
}
