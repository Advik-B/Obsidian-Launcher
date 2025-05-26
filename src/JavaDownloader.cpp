// src/JavaDownloader.cpp
#include <Launcher/JavaDownloader.hpp>
#include <Launcher/Utils/OS.hpp>
#include <Launcher/Utils/Crypto.hpp>
#include <Launcher/Http.hpp>
#include <Launcher/Utils/Logger.hpp>

#include <nlohmann/json.hpp> // Full include for json::parse and usage
#include <fstream>
#include <filesystem>

// using json = nlohmann::json; // Already in Http.hpp and Version.hpp if included, but fine here

namespace Launcher {

JavaDownloader::JavaDownloader() {
    m_logger = Utils::Logger::GetOrCreateLogger("JavaDownloader");
    m_logger->trace("Initialized.");
}

nlohmann::json JavaDownloader::fetchMojangJavaManifest() {
    const std::string javaManifestUrl = "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";
    m_logger->info("Fetching Mojang Java runtime manifest from: {}", javaManifestUrl);
    cpr::Response r_manifest = Http::Get(cpr::Url{javaManifestUrl}); // Http::Get now logs trace
    if (r_manifest.status_code != 200) {
        m_logger->error("Failed to download Java runtime manifest. Status: {}, URL: {}, Error: {}", r_manifest.status_code, javaManifestUrl, r_manifest.error.message);
        if (!r_manifest.text.empty() && r_manifest.status_code >= 400) {
            m_logger->error("Response: {}", r_manifest.text);
        }
        return nullptr;
    }
    m_logger->info("Successfully fetched Mojang Java manifest.");
    try {
        return nlohmann::json::parse(r_manifest.text);
    } catch (const nlohmann::json::parse_error& e) {
        m_logger->error("Failed to parse Java runtime manifest: {}", e.what());
        return nullptr;
    }
}

std::filesystem::path JavaDownloader::downloadJavaForMinecraftVersionMojang(const Version& mcVersion, const std::filesystem::path& baseDownloadDir) {
    if (!mcVersion.javaVersion) {
        m_logger->info("Minecraft version {} does not specify a Java version. Skipping Java download via Mojang.", mcVersion.id);
        return "";
    }

    const auto& requiredJava = *mcVersion.javaVersion;
    m_logger->info("Mojang Manifest - Required Java: Component '{}', Major Version '{}'", requiredJava.component, requiredJava.majorVersion);

    nlohmann::json javaManifestJson = fetchMojangJavaManifest();
    if (javaManifestJson.is_null()) {
        return ""; // fetchMojangJavaManifest already logged the error
    }

    Utils::OperatingSystem currentOS = Utils::getCurrentOS();
    Utils::Architecture currentArch = Utils::getCurrentArch();
    std::string osArchKey = Utils::getOSStringForJavaManifest(currentOS, currentArch);

    if (osArchKey == "unknown-os-arch-mojang") {
        m_logger->error("Mojang Manifest - Cannot determine OS/Arch string. OS: {}, Arch: {}", static_cast<int>(currentOS), static_cast<int>(currentArch));
        return "";
    }
    m_logger->info("Mojang Manifest - Determined OS/Arch key: {}", osArchKey);

    if (!javaManifestJson.contains(osArchKey) || !javaManifestJson.at(osArchKey).contains(requiredJava.component)) {
        m_logger->error("Mojang Manifest - Java runtime for OS/Arch '{}' and component '{}' not found.", osArchKey, requiredJava.component);
        return "";
    }

    const nlohmann::json& componentVersions = javaManifestJson.at(osArchKey).at(requiredJava.component);
    std::string downloadUrl;
    std::string expectedSha1;

    for (const auto& entry : componentVersions) {
        unsigned int entryMajorVersion = 0;
        if (entry.contains("version") && entry.at("version").contains("name")) {
            const auto& name_json = entry.at("version").at("name");
            if (name_json.is_number_unsigned()) {
                entryMajorVersion = name_json.get<unsigned int>();
            } else if (name_json.is_string()) {
                std::string name_str = name_json.get<std::string>();
                try {
                    entryMajorVersion = static_cast<unsigned int>(std::stoul(name_str));
                } catch (const std::exception&) {
                    size_t dot_pos = name_str.find('.');
                    try {
                        if (dot_pos != std::string::npos) {
                           entryMajorVersion = static_cast<unsigned int>(std::stoul(name_str.substr(0, dot_pos)));
                        } else {
                           entryMajorVersion = static_cast<unsigned int>(std::stoul(name_str));
                        }
                    } catch (const std::exception& e_parse) {
                         m_logger->warn("Could not parse major version from string: {} ({})", name_str, e_parse.what());
                    }
                }
            }
        }

        if (entryMajorVersion == requiredJava.majorVersion) {
            if (entry.contains("manifest") && entry.at("manifest").contains("url") && entry.at("manifest").contains("sha1")) {
                downloadUrl = entry.at("manifest").at("url").get<std::string>();
                expectedSha1 = entry.at("manifest").at("sha1").get<std::string>();
                break;
            }
        }
    }

    if (downloadUrl.empty()) {
        m_logger->error("Mojang Manifest - Could not find a download URL for Java component '{}' major version '{}' on '{}'.", requiredJava.component, requiredJava.majorVersion, osArchKey);
        return "";
    }
    m_logger->info("Mojang Manifest - Found Java download URL: {}", downloadUrl);

    if (!std::filesystem::exists(baseDownloadDir)) {
        if (!std::filesystem::create_directories(baseDownloadDir)) {
            m_logger->error("Failed to create Java download directory: {}", baseDownloadDir.string());
            return "";
        }
    }

    std::string filename = downloadUrl.substr(downloadUrl.find_last_of('/') + 1);
    std::filesystem::path downloadPath = baseDownloadDir / filename;

    m_logger->info("Mojang Manifest - Downloading Java to: {}...", downloadPath.string());
    cpr::Response r_download = Http::Download(downloadPath, cpr::Url{downloadUrl});

    if (r_download.status_code != 200 || !r_download.error.message.empty()) {
        m_logger->error("Mojang Manifest - Java archive download failed for URL: {}", downloadUrl); // Http::Download already logged details
        return "";
    }
    m_logger->info("Mojang Manifest - Java downloaded successfully.");

    m_logger->info("Mojang Manifest - Verifying SHA1 hash...");
    std::string actualSha1 = Utils::calculateFileSHA1(downloadPath.string());

    if (actualSha1.empty()) {
        m_logger->error("Mojang Manifest - SHA1 calculation failed for {}", downloadPath.string());
        if(std::filesystem::exists(downloadPath)) std::filesystem::remove(downloadPath);
        return "";
    }
    if (actualSha1 != expectedSha1) {
        m_logger->error("Mojang Manifest - SHA1 hash mismatch! Expected: {}, Actual: {}", expectedSha1, actualSha1);
        if(std::filesystem::exists(downloadPath)) std::filesystem::remove(downloadPath);
        return "";
    }
    m_logger->info("Mojang Manifest - SHA1 hash verified.");
    m_logger->info("Mojang Manifest - Java archive downloaded and verified: {}", downloadPath.string());
    return downloadPath;
}


std::filesystem::path JavaDownloader::downloadJavaForSpecificVersionAdoptium(const JavaVersion& requiredJava, const std::filesystem::path& baseDownloadDir) {
    m_logger->info("Adoptium API - Attempting to download Java. Required Major Version: {}", requiredJava.majorVersion);

    Utils::OperatingSystem currentOS = Utils::getCurrentOS();
    Utils::Architecture currentArch = Utils::getCurrentArch();
    std::string adoptiumOS = Utils::getOSStringForAdoptium(currentOS);
    std::string adoptiumArch = Utils::getArchStringForAdoptium(currentArch);

    if (adoptiumOS.empty() || adoptiumArch.empty()) {
        m_logger->error("Adoptium API - Could not determine OS/Arch strings.");
        return "";
    }
    m_logger->info("Adoptium API - OS: {}, Arch: {}", adoptiumOS, adoptiumArch);

    std::string imageType = "jre";
    std::string apiUrl = "https://api.adoptium.net/v3/assets/latest/" +
                         std::to_string(requiredJava.majorVersion) + "/hotspot";

    cpr::Parameters params = {
        {"architecture", adoptiumArch},
        {"heap_size", "normal"},
        {"image_type", imageType},
        {"os", adoptiumOS},
        {"vendor", "eclipse"}
    };

    m_logger->info("Adoptium API - Querying: {} with params: arch={}, os={}", apiUrl, adoptiumArch, adoptiumOS);
    cpr::Response r_api = Http::Get(cpr::Url{apiUrl}, params);

    if (r_api.status_code != 200) {
        m_logger->error("Adoptium API - Failed to query. Status: {}, URL: {}, Error: {}", r_api.status_code, apiUrl, r_api.error.message);
        if(!r_api.text.empty() && r_api.status_code >= 400){ m_logger->error("Response: {}", r_api.text); }
        return "";
    }
    m_logger->info("Adoptium API - Successfully queried API.");

    nlohmann::json apiResponseJson;
    try {
        apiResponseJson = nlohmann::json::parse(r_api.text);
    } catch (const nlohmann::json::parse_error& e) {
        m_logger->error("Adoptium API - Failed to parse response: {}. Response Text: {}", e.what(), r_api.text);
        return "";
    }

    if (!apiResponseJson.is_array() || apiResponseJson.empty()) {
        m_logger->error("Adoptium API - No suitable builds or unexpected format. Response: {}", apiResponseJson.dump(2));
        return "";
    }

    const nlohmann::json& firstBuild = apiResponseJson[0];
     if (!firstBuild.contains("binary") || !firstBuild["binary"].contains("package") ||
        !firstBuild["binary"]["package"].contains("link") ||
        !firstBuild["binary"]["package"].contains("name") ||
        !firstBuild["binary"]["package"].contains("checksum")) {
        m_logger->error("Adoptium API - Response missing required fields. Build Entry: {}", firstBuild.dump(2));
        return "";
    }

    std::string downloadUrl = firstBuild["binary"]["package"]["link"].get<std::string>();
    std::string filename = firstBuild["binary"]["package"]["name"].get<std::string>();
    std::string expectedSha256 = firstBuild["binary"]["package"]["checksum"].get<std::string>();

    m_logger->info("Adoptium API - Found Java download URL: {}", downloadUrl);
    m_logger->info("Filename: {}, Expected SHA256: {}", filename, expectedSha256);

    if (!std::filesystem::exists(baseDownloadDir)) {
        if (!std::filesystem::create_directories(baseDownloadDir)) {
            m_logger->error("Failed to create Java download directory: {}", baseDownloadDir.string());
            return "";
        }
    }
    std::filesystem::path downloadPath = baseDownloadDir / filename;

    m_logger->info("Adoptium API - Downloading Java to: {}...", downloadPath.string());
    cpr::Response r_download = Http::Download(downloadPath, cpr::Url{downloadUrl});

    if (r_download.status_code != 200 || !r_download.error.message.empty()) {
        m_logger->error("Adoptium API - Java archive download failed for URL: {}", downloadUrl);
        return "";
    }
    m_logger->info("Adoptium API - Java downloaded successfully.");

    m_logger->info("Adoptium API - Verifying SHA256 hash...");
    std::string actualSha256 = Utils::calculateFileSHA256(downloadPath.string());
    if (actualSha256.empty()) {
        m_logger->error("Adoptium API - SHA256 calculation failed for {}", downloadPath.string());
        if(std::filesystem::exists(downloadPath)) std::filesystem::remove(downloadPath);
        return "";
    }

    if (actualSha256 != expectedSha256) {
        m_logger->error("Adoptium API - SHA256 hash mismatch! Expected: {}, Actual: {}", expectedSha256, actualSha256);
        if(std::filesystem::exists(downloadPath)) std::filesystem::remove(downloadPath);
        return "";
    }
    m_logger->info("Adoptium API - SHA256 hash verified.");
    m_logger->info("Adoptium API - Java archive downloaded and verified: {}", downloadPath.string());
    return downloadPath;
}

} // namespace Launcher