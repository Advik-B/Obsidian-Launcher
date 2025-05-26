// include/Launcher/JavaDownloader.hpp
#ifndef JAVA_DOWNLOADER_HPP
#define JAVA_DOWNLOADER_HPP

#include <string>
#include <Launcher/Types/JavaVersion.hpp>
#include <Launcher/Types/Version.hpp>
#include <filesystem>
#include <spdlog/logger.h> // For std::shared_ptr<spdlog::logger>
#include <nlohmann/json_fwd.hpp> // Forward declare json

namespace Launcher {

    class JavaDownloader {
    public:
        JavaDownloader();

        std::filesystem::path downloadJavaForMinecraftVersionMojang(const Version& mcVersion, const std::filesystem::path& baseDownloadDir);
        std::filesystem::path downloadJavaForSpecificVersionAdoptium(const JavaVersion& requiredJava, const std::filesystem::path& baseDownloadDir);

    private:
        nlohmann::json fetchMojangJavaManifest(); // Return type nlohmann::json
        std::shared_ptr<spdlog::logger> m_logger;
    };

} // namespace Launcher

#endif //JAVA_DOWNLOADER_HPP