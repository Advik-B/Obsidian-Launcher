// include/Launcher/JavaManager.hpp
#ifndef JAVA_MANAGER_HPP
#define JAVA_MANAGER_HPP

#include <Launcher/Types/Version.hpp>
#include <Launcher/Config.hpp>
// #include <Launcher/JavaDownloader.hpp> // Forward declare instead if only pointer/ref used
#include <filesystem>
#include <optional>
#include <spdlog/logger.h>
#include <vector>

#include "JavaDownloader.hpp"

namespace Launcher {

    class HttpManager;    // Forward declaration
    class JavaDownloader; // Forward declaration

    struct JavaRuntime {
        std::filesystem::path homePath;
        std::filesystem::path javaExecutablePath;
        unsigned int majorVersion;
        std::string componentName;
        std::string source;
    };

    class JavaManager {
    public:
        JavaManager(const Config& config, HttpManager& httpManager);
        std::optional<JavaRuntime> ensureJavaForMinecraftVersion(const Version& mcVersion);
        bool extractJavaArchive(const std::filesystem::path& archivePath, const std::filesystem::path& extractionDir, const std::string& runtimeNameForPath);
        std::filesystem::path findJavaExecutable(const std::filesystem::path& extractedJavaHome);
        std::vector<JavaRuntime> getAvailableRuntimes() const;

    private:
        const Config& m_config;
        HttpManager& m_httpManager;
        JavaDownloader m_javaDownloader;
        std::vector<JavaRuntime> m_availableRuntimes;
        std::shared_ptr<spdlog::logger> m_logger;

        std::filesystem::path getExtractionPathForRuntime(const JavaVersion& javaVersion);
        void scanForExistingRuntimes();
    };

} // namespace Launcher
#endif //JAVA_MANAGER_HPP