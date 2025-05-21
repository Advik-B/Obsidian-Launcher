// include/Launcher/JavaManager.hpp
#ifndef JAVA_MANAGER_HPP
#define JAVA_MANAGER_HPP

#include <Launcher/Types/Version.hpp>
#include <Launcher/Config.hpp> // We'll create this soon, or use existing paths
#include <Launcher/JavaDownloader.hpp>
#include <filesystem>
#include <optional>

namespace Launcher {

    // Represents an installed/available Java runtime
    struct JavaRuntime {
        std::filesystem::path homePath;         // Path to the root of the extracted Java runtime
        std::filesystem::path javaExecutablePath; // Path to the java/javaw executable
        unsigned int majorVersion;
        std::string componentName; // e.g., "jre-legacy", "java-runtime-gamma"
        std::string source; // "mojang", "adoptium", "user_provided"
    };

    class JavaManager {
    public:
        JavaManager(const Config& config);

        // Ensures a suitable Java runtime is available for the given Minecraft version.
        // Downloads and extracts if necessary.
        // Returns an optional path to the Java executable.
        std::optional<JavaRuntime> ensureJavaForMinecraftVersion(const Version& mcVersion);

        // Extracts a downloaded Java archive.
        // Returns true on success, false on failure.
        bool extractJavaArchive(const std::filesystem::path& archivePath, const std::filesystem::path& extractionDir, const std::string& runtimeNameForPath);

        // Finds the Java executable within an extracted Java runtime directory.
        // Returns the path to the executable or an empty path if not found.
        std::filesystem::path findJavaExecutable(const std::filesystem::path& extractedJavaHome);

        // Lists available Java runtimes managed by the launcher
        std::vector<JavaRuntime> getAvailableRuntimes() const;

    private:
        const Config& m_config;
        JavaDownloader m_javaDownloader;
        std::vector<JavaRuntime> m_availableRuntimes; // Cache of discovered runtimes

        std::filesystem::path getExtractionPathForRuntime(const JavaVersion& javaVersion);
        std::filesystem::path getExtractionPathForRuntime(const std::string& component, unsigned int majorVersion);
        void scanForExistingRuntimes(); // To discover already extracted runtimes on startup
    };

} // namespace Launcher

#endif //JAVA_MANAGER_HPP