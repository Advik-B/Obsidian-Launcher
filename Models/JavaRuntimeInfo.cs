namespace ObsidianLauncher.Models
{
    public class JavaRuntimeInfo
    {
        public string HomePath { get; set; }
        public string JavaExecutablePath { get; set; }
        public uint MajorVersion { get; set; }
        public string ComponentName { get; set; }
        public string Source { get; set; } // e.g., "mojang", "adoptium", "user_provided"
    }
}