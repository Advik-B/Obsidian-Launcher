// include/Launcher/JavaDownloader.hpp
#ifndef JAVA_DOWNLOADER_HPP
#define JAVA_DOWNLOADER_HPP

#include <string>
#include <Launcher/Types/JavaVersion.hpp>
#include <Launcher/Types/Version.hpp>
#include <filesystem>
#include <spdlog/logger.h>
#include <nlohmann/json_fwd.hpp>

namespace Launcher {

    class HttpManager; // Forward declaration

    class JavaDownloader {
    public:
        JavaDownloader(HttpManager& httpManager); // Constructor takes HttpManager

        std::filesystem::path downloadJavaForMinecraftVersionMojang(const Version& mcVersion, const std::filesystem::path& baseDownloadDir);
        std::filesystem::path downloadJavaForSpecificVersionAdoptium(const JavaVersion& requiredJava, const std::filesystem::path& baseDownloadDir);

    private:
        nlohmann::json fetchMojangJavaManifest();
        HttpManager& m_httpManager; // Store reference to HttpManager
        std::shared_ptr<spdlog::logger> m_logger;
    };

} // namespace Launcher

#endif //JAVA_DOWNLOADER_HPP