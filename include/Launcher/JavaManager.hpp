// include/Launcher/JavaManager.hpp
#ifndef JAVA_MANAGER_HPP
#define JAVA_MANAGER_HPP

#include <Launcher/Types/Version.hpp>
#include <Launcher/Config.hpp>
#include <Launcher/JavaDownloader.hpp>
#include <filesystem>
#include <optional>
#include <vector>
#include <spdlog/logger.h> // For std::shared_ptr<spdlog::logger>

namespace Launcher {

    struct JavaRuntime {
        std::filesystem::path homePath;
        std::filesystem::path javaExecutablePath;
        unsigned int majorVersion;
        std::string componentName;
        std::string source;
    };

    class JavaManager {
    public:
        JavaManager(const Config& config);
        std::optional<JavaRuntime> ensureJavaForMinecraftVersion(const Version& mcVersion);
        bool extractJavaArchive(const std::filesystem::path& archivePath, const std::filesystem::path& extractionDir, const std::string& runtimeNameForPath);
        std::filesystem::path findJavaExecutable(const std::filesystem::path& extractedJavaHome);
        std::vector<JavaRuntime> getAvailableRuntimes() const;

    private:
        const Config& m_config;
        JavaDownloader m_javaDownloader;
        std::vector<JavaRuntime> m_availableRuntimes;
        std::shared_ptr<spdlog::logger> m_logger; // Member logger

        std::filesystem::path getExtractionPathForRuntime(const JavaVersion& javaVersion);
        // std::filesystem::path getExtractionPathForRuntime(const std::string& component, unsigned int majorVersion); // Already defined
        void scanForExistingRuntimes();
    };

} // namespace Launcher

#endif //JAVA_MANAGER_HPP