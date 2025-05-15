#include <iostream>
#include <cpr/cpr.h>
#include <nlohmann/json.hpp>
#include <Launcher/Types/Version.hpp>
#include <Launcher/Types/VersionMeta.hpp>

using json = nlohmann::json;

int main() {
    const auto manifest_json = json::parse(cpr::Get(cpr::Url{"https://launchermeta.mojang.com/mc/game/version_manifest_v2.json"}).text);

    // Example: Parse a specific version (e.g., "25w18a" or "rd-132211")
    // Find the URL for "25w18a"
    std::string version_id_to_parse = "25w18a"; // or "rd-132211" or any other
    std::string version_url;

    for (const auto& ver_meta_json : manifest_json["versions"]) {
        if (ver_meta_json["id"].get<std::string>() == version_id_to_parse) {
            version_url = ver_meta_json["url"].get<std::string>();
            break;
        }
    }

    if (version_url.empty()) {
        std::cerr << "Version " << version_id_to_parse << " not found in manifest." << std::endl;
        return 1;
    }

    const auto version_json_text = cpr::Get(cpr::Url{version_url}).text;
    const auto version_data_json = json::parse(version_json_text);
    Version parsed_version = Version::from_json(version_data_json);

    std::cout << "Successfully parsed version: " << parsed_version.id << std::endl;
    if (parsed_version.assetIndex) {
        std::cout << "Asset ID: " << parsed_version.assetIndex->id << std::endl;
    }
    if (parsed_version.mainClass) {
        std::cout << "Main class: " << *parsed_version.mainClass << std::endl;
    }
    if (!parsed_version.libraries.empty()) {
        std::cout << "First library: " << parsed_version.libraries[0].name << std::endl;
    }

    return 0;
}