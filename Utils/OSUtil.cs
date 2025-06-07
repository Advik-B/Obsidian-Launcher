using System.Runtime.InteropServices; // For RuntimeInformation
using Serilog; // For logging, if needed within this utility

using ObsidianLauncher.Enums;

namespace ObsidianLauncher.Utils
{
    public static class OsUtils
    {
        private static readonly ILogger _logger = Log.ForContext(typeof(OsUtils));

        /// <summary>
        /// Gets the current operating system.
        /// </summary>
        /// <returns>The current OperatingSystemType.</returns>
        public static OperatingSystemType GetCurrentOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return OperatingSystemType.Windows;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) // macOS
            {
                return OperatingSystemType.MacOS;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return OperatingSystemType.Linux;
            }
            _logger.Warning("Could not determine current OS. Defaulting to Unknown.");
            return OperatingSystemType.Unknown;
        }

        /// <summary>
        /// Gets the current processor architecture.
        /// </summary>
        /// <returns>The current ArchitectureType.</returns>
        public static ArchitectureType GetCurrentArchitecture()
        {
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X86:
                    return ArchitectureType.X86;
                case Architecture.X64:
                    return ArchitectureType.X64;
                case Architecture.Arm:
                    return ArchitectureType.Arm; // 32-bit ARM
                case Architecture.Arm64:
                    return ArchitectureType.Arm64;
                default:
                    _logger.Warning("Could not determine current architecture. Defaulting to Unknown. Actual: {ActualArch}", RuntimeInformation.ProcessArchitecture);
                    return ArchitectureType.Unknown;
            }
        }

        /// <summary>
        /// Gets the OS and architecture string formatted for Mojang's Java runtime manifest.
        /// </summary>
        /// <returns>A string like "windows-x64", "mac-os", "linux", etc., or "unknown-os-arch-mojang" if undetermined.</returns>
        public static string GetOSStringForJavaManifest()
        {
            var os = GetCurrentOS();
            var arch = GetCurrentArchitecture();

            switch (os)
            {
                case OperatingSystemType.Windows:
                    switch (arch)
                    {
                        case ArchitectureType.X64:
                            return "windows-x64";
                        case ArchitectureType.X86:
                            return "windows-x86"; // 32-bit
                        case ArchitectureType.Arm64:
                            return "windows-arm64";
                    }

                    _logger.Warning("Unsupported Windows architecture for Mojang manifest: {Architecture}", arch);
                    break;
                case OperatingSystemType.MacOS:
                    switch (arch)
                    {
                        // Mojang manifest uses "mac-os" for Intel (x64) and "mac-os-arm64" for Apple Silicon (Arm64)
                        case ArchitectureType.X64:
                            return "mac-os";
                        case ArchitectureType.Arm64:
                            return "mac-os-arm64";
                    }

                    _logger.Warning("Unsupported macOS architecture for Mojang manifest: {Architecture}", arch);
                    break;
                case OperatingSystemType.Linux:
                    switch (arch)
                    {
                        case ArchitectureType.X64:
                            return "linux";
                        case ArchitectureType.Arm64:
                            return "linux-aarch64"; // Check Mojang's exact naming for Linux ARM
                    }

                    // Add other Linux arch variants if Mojang's manifest supports them (e.g., "linux-arm32")
                    _logger.Warning("Unsupported Linux architecture for Mojang manifest: {Architecture}", arch);
                    break;
                case OperatingSystemType.Unknown:
                default:
                    _logger.Warning("Unknown OS for Mojang manifest: {OperatingSystem}", os);
                    break;
            }
            return "unknown-os-arch-mojang"; // Fallback
        }

        /// <summary>
        /// Gets the OS string formatted for the Adoptium API.
        /// </summary>
        /// <returns>A string like "windows", "mac", "linux", or an empty string if undetermined.</returns>
        public static string GetOSStringForAdoptium()
        {
            OperatingSystemType os = GetCurrentOS();
            switch (os)
            {
                case OperatingSystemType.Windows: return "windows";
                case OperatingSystemType.MacOS: return "mac";
                case OperatingSystemType.Linux: return "linux";
                default:
                    _logger.Warning("Unknown OS for Adoptium API: {OperatingSystem}", os);
                    return ""; // Or throw, or a specific "unknown" string if the API handles it
            }
        }

        /// <summary>
        /// Gets the architecture string formatted for the Adoptium API.
        /// </summary>
        /// <returns>A string like "x64", "x86", "aarch64", "arm", or an empty string if undetermined.</returns>
        public static string GetArchStringForAdoptium()
        {
            ArchitectureType arch = GetCurrentArchitecture();
            switch (arch)
            {
                case ArchitectureType.X64: return "x64";
                case ArchitectureType.X86: return "x86"; // Adoptium uses "x86" for 32-bit
                case ArchitectureType.Arm64: return "aarch64";
                case ArchitectureType.Arm: return "arm"; // Adoptium uses "arm" for 32-bit ARM
                default:
                    _logger.Warning("Unknown architecture for Adoptium API: {Architecture}", arch);
                    return ""; // Or throw, or a specific "unknown" string if the API handles it
            }
        }
    }
}