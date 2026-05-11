namespace ObsidianLauncher.Models;

public class JavaRuntimeInfo
{
    public required string HomePath { get; set; }
    public required string JavaExecutablePath { get; set; }
    public uint MajorVersion { get; set; }
    public required string ComponentName { get; set; }
    public required string Source { get; set; } // e.g., "mojang", "adoptium", "user_provided"
}