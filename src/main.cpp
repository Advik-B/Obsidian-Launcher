// src/main.cpp
#include <iostream>
#include <string>
#include <vector>
#include <optional>
#include <filesystem> // For std::filesystem::path

// Remove direct cpr/cpr.h include if only using the wrapper
// #include <cpr/cpr.h>
#include <nlohmann/json.hpp>

#include <Launcher/Http.hpp> // Our HTTP wrapper
#include <Launcher/Types/Version.hpp>
#include <Launcher/Types/VersionMeta.hpp>
#include <Launcher/JavaDownloader.hpp>

using json = nlohmann::json;

int main() {
    // No explicit initialization of SSL opts needed here anymore
    // if Http.cpp handles it statically.

    // Fetch the main version manifest from Mojang using the wrapper
    cpr::Response manifest_response = Launcher::Http::Get(cpr::Url{"https://launchermeta.mojang.com/mc/game/version_manifest_v2.json"});
    if (manifest_response.status_code != 200) {
        std::cerr << "Failed to fetch version manifest: " << manifest_response.status_code
                  << " - " << manifest_response.error.message << std::endl;
        if(!manifest_response.text.empty() && manifest_response.status_code >= 400) {
            std::cerr << "Response Body: " << manifest_response.text << std::endl;
        }
        return 1;
    }
    json manifest_json;
    try {
        manifest_json = json::parse(manifest_response.text);
    } catch (const json::parse_error& e) {
        std::cerr << "Failed to parse version manifest JSON: " << e.what() << std::endl;
        return 1;
    }

    // --- Select a Minecraft version to process ---
    std::string version_id_to_parse = "1.20.4";
    // ... (rest of version selection logic) ...
     std::string version_url;
    if (manifest_json.contains("versions") && manifest_json["versions"].is_array()) {
        for (const auto& ver_meta_json : manifest_json["versions"]) {
            if (ver_meta_json.contains("id") && ver_meta_json["id"].get<std::string>() == version_id_to_parse) {
                if (ver_meta_json.contains("url")) {
                    version_url = ver_meta_json["url"].get<std::string>();
                    break;
                }
            }
        }
    }

    if (version_url.empty()) {
        std::cerr << "Version " << version_id_to_parse << " not found or URL missing in manifest." << std::endl;
        return 1;
    }


    // Fetch the detailed JSON for the selected Minecraft version
    std::cout << "Fetching version details for " << version_id_to_parse << " from " << version_url << std::endl;
    cpr::Response version_details_response = Launcher::Http::Get(cpr::Url{version_url});
    if (version_details_response.status_code != 200) {
        std::cerr << "Failed to fetch version details for " << version_id_to_parse << ": "
                  << version_details_response.status_code << " - " << version_details_response.error.message << std::endl;
        if(!version_details_response.text.empty() && version_details_response.status_code >= 400) {
            std::cerr << "Response Body: " << version_details_response.text << std::endl;
        }
        return 1;
    }
    json version_data_json;
    try {
        version_data_json = json::parse(version_details_response.text);
    } catch(const json::parse_error& e) {
        std::cerr << "Failed to parse version JSON for " << version_id_to_parse << ": " << e.what() << std::endl;
        return 1;
    }

    Version parsed_minecraft_version = Version::from_json(version_data_json);

    std::cout << "Successfully parsed Minecraft version: " << parsed_minecraft_version.id << std::endl;
    if (parsed_minecraft_version.javaVersion) {
        std::cout << "  Requires Java Component: " << parsed_minecraft_version.javaVersion->component
                  << ", Major Version: " << parsed_minecraft_version.javaVersion->majorVersion << std::endl;
    } else {
        std::cout << "  No specific Java version explicitly listed in this version's manifest." << std::endl;
    }

    // --- Java Download Logic ---
    Launcher::JavaDownloader java_downloader;
    std::filesystem::path java_runtimes_base_dir = "./java_runtimes";
    std::filesystem::path downloaded_java_archive_path;

    if (parsed_minecraft_version.javaVersion) {
        const auto& required_java = *parsed_minecraft_version.javaVersion;

        std::cout << "\n--- Attempting Java Download via Adoptium API ---" << std::endl;
        downloaded_java_archive_path = java_downloader.downloadJavaForSpecificVersionAdoptium(required_java, java_runtimes_base_dir / "adoptium");

        if (downloaded_java_archive_path.empty()) {
            std::cerr << "Java download via Adoptium API failed or was skipped for "
                      << required_java.component << " v" << required_java.majorVersion << "." << std::endl;

            std::cout << "\n--- Attempting Java Download via Mojang Manifest as fallback/alternative ---" << std::endl;
            downloaded_java_archive_path = java_downloader.downloadJavaForMinecraftVersionMojang(parsed_minecraft_version, java_runtimes_base_dir / "mojang");

            if (downloaded_java_archive_path.empty()) {
                std::cerr << "Java download via Mojang Manifest also failed or was skipped for "
                          << required_java.component << " v" << required_java.majorVersion << "." << std::endl;
            }
        }
    } else {
        std::cout << "\nNo specific Java version required by " << parsed_minecraft_version.id
                  << " in its manifest. Consider using a default (e.g., Java 17)." << std::endl;
    }

    if (!downloaded_java_archive_path.empty()) {
        std::cout << "\nJava runtime archive is available at: " << downloaded_java_archive_path.string() << std::endl;
        std::cout << "Next steps would be to extract this archive to a suitable location and use it to launch Minecraft." << std::endl;
    } else {
        std::cout << "\nFailed to obtain a suitable Java runtime for Minecraft version " << parsed_minecraft_version.id << std::endl;
    }

    return 0;
}