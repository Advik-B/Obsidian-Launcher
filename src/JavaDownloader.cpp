// src/JavaDownloader.cpp
#include <Launcher/JavaDownloader.hpp>
#include <Launcher/Utils/OS.hpp>
#include <Launcher/Utils/Crypto.hpp>
#include <Launcher/Http.hpp> // <--- Use the HTTP wrapper

#include <nlohmann/json.hpp>
#include <iostream>
#include <fstream>
#include <filesystem>

using json = nlohmann::json;

namespace Launcher {

JavaDownloader::JavaDownloader() = default;

nlohmann::json JavaDownloader::fetchMojangJavaManifest() {
    const std::string javaManifestUrl = "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";
    cpr::Response r_manifest = Http::Get(cpr::Url{javaManifestUrl}); // Use wrapper
    if (r_manifest.status_code != 200) {
        std::cerr << "Failed to download Java runtime manifest: " << r_manifest.status_code << std::endl;
        std::cerr << "URL: " << javaManifestUrl << std::endl;
        std::cerr << "Error: " << r_manifest.error.message << std::endl;
        if (!r_manifest.text.empty() && r_manifest.status_code >= 400) {
            std::cerr << "Response: " << r_manifest.text << std::endl;
        }
        return nullptr;
    }
    try {
        return json::parse(r_manifest.text);
    } catch (const json::parse_error& e) {
        std::cerr << "Failed to parse Java runtime manifest: " << e.what() << std::endl;
        return nullptr;
    }
}

std::filesystem::path JavaDownloader::downloadJavaForMinecraftVersionMojang(const Version& mcVersion, const std::filesystem::path& baseDownloadDir) {
    if (!mcVersion.javaVersion) {
        std::cout << "Minecraft version " << mcVersion.id << " does not specify a Java version. Skipping Java download via Mojang." << std::endl;
        return "";
    }

    const auto& requiredJava = *mcVersion.javaVersion;
    std::cout << "Mojang Manifest - Required Java: Component '" << requiredJava.component
              << "', Major Version '" << requiredJava.majorVersion << "'" << std::endl;

    json javaManifestJson = fetchMojangJavaManifest();
    if (javaManifestJson.is_null()) {
        return "";
    }

    Utils::OperatingSystem currentOS = Utils::getCurrentOS();
    Utils::Architecture currentArch = Utils::getCurrentArch();
    std::string osArchKey = Utils::getOSStringForJavaManifest(currentOS, currentArch);

    if (osArchKey == "unknown-os-arch-mojang") {
        std::cerr << "Mojang Manifest - Cannot determine OS/Arch string. OS: "
                  << static_cast<int>(currentOS) << ", Arch: " << static_cast<int>(currentArch) << std::endl;
        return "";
    }
    std::cout << "Mojang Manifest - Determined OS/Arch key: " << osArchKey << std::endl;

    if (!javaManifestJson.contains(osArchKey) || !javaManifestJson.at(osArchKey).contains(requiredJava.component)) {
        std::cerr << "Mojang Manifest - Java runtime for OS/Arch '" << osArchKey << "' and component '"
                  << requiredJava.component << "' not found." << std::endl;
        return "";
    }

    const json& componentVersions = javaManifestJson.at(osArchKey).at(requiredJava.component);
    std::string downloadUrl;
    std::string expectedSha1;

    for (const auto& entry : componentVersions) {
        unsigned int entryMajorVersion = 0;
        if (entry.contains("version") && entry.at("version").contains("name")) {
            const auto& name_json = entry.at("version").at("name");
            if (name_json.is_number_unsigned()) {
                entryMajorVersion = name_json.get<unsigned int>();
            } else if (name_json.is_string()) {
                try {
                    entryMajorVersion = static_cast<unsigned int>(std::stoul(name_json.get<std::string>()));
                } catch (const std::exception&) {
                    std::string name_str = name_json.get<std::string>();
                    size_t dot_pos = name_str.find('.');
                    try {
                        if (dot_pos != std::string::npos) {
                           entryMajorVersion = static_cast<unsigned int>(std::stoul(name_str.substr(0, dot_pos)));
                        } else {
                           entryMajorVersion = static_cast<unsigned int>(std::stoul(name_str)); // If no dot, try full string
                        }
                    } catch (const std::exception& e_parse) {
                         std::cerr << "Warning: Could not parse major version from string: " << name_str << " (" << e_parse.what() << ")" << std::endl;
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
        std::cerr << "Mojang Manifest - Could not find a download URL for Java component '" << requiredJava.component
                  << "' major version '" << requiredJava.majorVersion << "' on '" << osArchKey << "'." << std::endl;
        return "";
    }

    std::cout << "Mojang Manifest - Found Java download URL: " << downloadUrl << std::endl;

    if (!std::filesystem::exists(baseDownloadDir)) {
        if (!std::filesystem::create_directories(baseDownloadDir)) {
            std::cerr << "Failed to create Java download directory: " << baseDownloadDir << std::endl;
            return "";
        }
    }

    std::string filename = downloadUrl.substr(downloadUrl.find_last_of('/') + 1);
    std::filesystem::path downloadPath = baseDownloadDir / filename;

    std::cout << "Mojang Manifest - Downloading Java to: " << downloadPath.string() << "..." << std::endl;
    // Use Http::Download which takes a path
    cpr::Response r_download = Http::Download(downloadPath, cpr::Url{downloadUrl});

    if (r_download.status_code != 200 || !r_download.error.message.empty()) { // Check error message too
        std::cerr << "Mojang Manifest - Failed to download Java archive: " << r_download.status_code << std::endl;
        std::cerr << "URL: " << downloadUrl << std::endl;
        std::cerr << "Error: " << r_download.error.message << std::endl;
        if (!r_download.text.empty() && r_download.status_code >= 400) {
            std::cerr << "Response Body: " << r_download.text << std::endl;
        }
        // Http::Download wrapper already tries to remove on failure if file was opened.
        // If the wrapper couldn't open the file, downloadPath might not exist.
        if(std::filesystem::exists(downloadPath)) std::filesystem::remove(downloadPath);
        return "";
    }
    std::cout << "Mojang Manifest - Java downloaded successfully." << std::endl;

    std::cout << "Mojang Manifest - Verifying SHA1 hash..." << std::endl;
    std::string actualSha1 = Utils::calculateFileSHA1(downloadPath.string());

    if (actualSha1.empty()) {
        std::cerr << "Mojang Manifest - SHA1 calculation failed for " << downloadPath.string() << std::endl;
        std::filesystem::remove(downloadPath);
        return "";
    }
    if (actualSha1 != expectedSha1) {
        std::cerr << "Mojang Manifest - SHA1 hash mismatch!" << std::endl;
        std::cerr << "Expected: " << expectedSha1 << std::endl;
        std::cerr << "Actual:   " << actualSha1 << std::endl;
        std::filesystem::remove(downloadPath);
        return "";
    }
    std::cout << "Mojang Manifest - SHA1 hash verified." << std::endl;
    std::cout << "Mojang Manifest - Java archive downloaded and verified: " << downloadPath.string() << std::endl;
    return downloadPath;
}


std::filesystem::path JavaDownloader::downloadJavaForSpecificVersionAdoptium(const JavaVersion& requiredJava, const std::filesystem::path& baseDownloadDir) {
    std::cout << "Adoptium API - Attempting to download Java." << std::endl;
    std::cout << "Adoptium API - Required Java Major Version: " << requiredJava.majorVersion << std::endl;

    Utils::OperatingSystem currentOS = Utils::getCurrentOS();
    Utils::Architecture currentArch = Utils::getCurrentArch();
    std::string adoptiumOS = Utils::getOSStringForAdoptium(currentOS);
    std::string adoptiumArch = Utils::getArchStringForAdoptium(currentArch);

    if (adoptiumOS.empty() || adoptiumArch.empty()) {
        std::cerr << "Adoptium API - Could not determine OS/Arch strings." << std::endl;
        return "";
    }
    std::cout << "Adoptium API - OS: " << adoptiumOS << ", Arch: " << adoptiumArch << std::endl;

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

    std::cout << "Adoptium API - Querying: " << apiUrl << std::endl;
    cpr::Response r_api = Http::Get(cpr::Url{apiUrl}, params); // Use wrapper

    if (r_api.status_code != 200) {
        std::cerr << "Adoptium API - Failed to query: " << r_api.status_code << std::endl;
        std::cerr << "URL: " << apiUrl << " Params: arch=" << adoptiumArch << ", os=" << adoptiumOS << std::endl;
        std::cerr << "Error: " << r_api.error.message << std::endl;
        if(!r_api.text.empty() && r_api.status_code >= 400){ std::cerr << "Response: " << r_api.text << std::endl; }
        return "";
    }

    json apiResponseJson;
    try {
        apiResponseJson = json::parse(r_api.text);
    } catch (const json::parse_error& e) {
        std::cerr << "Adoptium API - Failed to parse response: " << e.what() << std::endl;
        std::cerr << "Response Text: " << r_api.text << std::endl;
        return "";
    }

    if (!apiResponseJson.is_array() || apiResponseJson.empty()) {
        std::cerr << "Adoptium API - No suitable builds or unexpected format." << std::endl;
        std::cerr << "Response: " << apiResponseJson.dump(2) << std::endl;
        return "";
    }

    const json& firstBuild = apiResponseJson[0];
     if (!firstBuild.contains("binary") || !firstBuild["binary"].contains("package") ||
        !firstBuild["binary"]["package"].contains("link") ||
        !firstBuild["binary"]["package"].contains("name") ||
        !firstBuild["binary"]["package"].contains("checksum")) {
        std::cerr << "Adoptium API - Response missing required fields." << std::endl;
        std::cerr << "Build Entry: " << firstBuild.dump(2) << std::endl;
        return "";
    }

    std::string downloadUrl = firstBuild["binary"]["package"]["link"].get<std::string>();
    std::string filename = firstBuild["binary"]["package"]["name"].get<std::string>();
    std::string expectedSha256 = firstBuild["binary"]["package"]["checksum"].get<std::string>();

    std::cout << "Adoptium API - Found Java download URL: " << downloadUrl << std::endl;
    std::cout << "Filename: " << filename << std::endl;
    std::cout << "Expected SHA256: " << expectedSha256 << std::endl;

    if (!std::filesystem::exists(baseDownloadDir)) {
        if (!std::filesystem::create_directories(baseDownloadDir)) {
            std::cerr << "Failed to create Java download directory: " << baseDownloadDir << std::endl;
            return "";
        }
    }
    std::filesystem::path downloadPath = baseDownloadDir / filename;

    std::cout << "Adoptium API - Downloading Java to: " << downloadPath.string() << "..." << std::endl;
    // Use Http::Download which takes a path
    cpr::Response r_download = Http::Download(downloadPath, cpr::Url{downloadUrl});

    if (r_download.status_code != 200 || !r_download.error.message.empty()) { // Check error message too
        std::cerr << "Adoptium API - Failed to download Java archive: " << r_download.status_code << std::endl;
        std::cerr << "URL: " << downloadUrl << std::endl;
        std::cerr << "Error: " << r_download.error.message << std::endl;
        if (!r_download.text.empty() && r_download.status_code >=400) {
             std::cerr << "Response Body: " << r_download.text << std::endl;
        }
        if(std::filesystem::exists(downloadPath)) std::filesystem::remove(downloadPath);
        return "";
    }
    std::cout << "Adoptium API - Java downloaded successfully." << std::endl;

    std::cout << "Adoptium API - Verifying SHA256 hash..." << std::endl;
    std::string actualSha256 = Utils::calculateFileSHA256(downloadPath.string());
    if (actualSha256.empty()) {
        std::cerr << "Adoptium API - SHA256 calculation failed for " << downloadPath.string() << std::endl;
        std::filesystem::remove(downloadPath);
        return "";
    }

    if (actualSha256 != expectedSha256) {
        std::cerr << "Adoptium API - SHA256 hash mismatch!" << std::endl;
        std::cerr << "Expected: " << expectedSha256 << std::endl;
        std::cerr << "Actual:   " << actualSha256 << std::endl;
        std::filesystem::remove(downloadPath);
        return "";
    }
    std::cout << "Adoptium API - SHA256 hash verified." << std::endl;
    std::cout << "Adoptium API - Java archive downloaded and verified: " << downloadPath.string() << std::endl;
    return downloadPath;
}

} // namespace Launcher