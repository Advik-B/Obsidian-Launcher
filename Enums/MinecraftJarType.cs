// Enums/MinecraftJarType.cs

namespace ObsidianLauncher.Enums
{
    /// <summary>
    /// Represents the different types of downloadable Minecraft JARs or related files
    /// as typically found in the "downloads" section of a version manifest.
    /// </summary>
    public enum MinecraftJarType
    {
        /// <summary>
        /// The main Minecraft client JAR file.
        /// JSON key: "client"
        /// </summary>
        Client,

        /// <summary>
        /// The Minecraft server JAR file.
        /// JSON key: "server"
        /// </summary>
        Server,

        /// <summary>
        /// The client mappings file (e.g., for ProGuard/Obfuscation).
        /// JSON key: "client_mappings"
        /// </summary>
        ClientMappings,

        /// <summary>
        /// The server mappings file.
        /// JSON key: "server_mappings"
        /// </summary>
        ServerMappings,

        // Add other types here if Mojang introduces them, e.g., "windows_server" for Bedrock Edition related things,
        // though that's usually separate from Java Edition manifests.

        /// <summary>
        /// Represents an unknown or unsupported JAR type.
        /// This can be used as a default or error value.
        /// </summary>
        Unknown
    }
}