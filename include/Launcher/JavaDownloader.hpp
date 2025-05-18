// include/Launcher/JavaDownloader.hpp
#ifndef JAVA_DOWNLOADER_HPP
#define JAVA_DOWNLOADER_HPP

#include <string>
#include <Launcher/Types/JavaVersion.hpp> // For JavaVersion struct
#include <Launcher/Types/Version.hpp>     // For Version struct
#include <filesystem>

namespace Launcher {

    class JavaDownloader {
    public:
        JavaDownloader();

        // Downloads Java based on Minecraft version requirements using Mojang's manifest
        // Returns the path to the downloaded (but not yet extracted) Java archive, or an empty path on failure.
        std::filesystem::path downloadJavaForMinecraftVersionMojang(const Version& mcVersion, const std::filesystem::path& baseDownloadDir);

        // Downloads Java based on a specific JavaVersion requirement using Adoptium API
        // Returns the path to the downloaded (but not yet extracted) Java archive, or an empty path on failure.
        std::filesystem::path downloadJavaForSpecificVersionAdoptium(const JavaVersion& requiredJava, const std::filesystem::path& baseDownloadDir);

    private:
        // Helper to get the Java runtime manifest from Mojang
        nlohmann::json fetchMojangJavaManifest();
    };

} // namespace Launcher

#endif //JAVA_DOWNLOADER_HPP