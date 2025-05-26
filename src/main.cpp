// src/main.cpp
#include <Launcher/Http.hpp>
#include <Launcher/Types/Version.hpp>
#include <Launcher/Types/VersionMeta.hpp> // Not directly used here, but good include
#include <Launcher/Config.hpp>
#include <Launcher/JavaManager.hpp>
#include <Launcher/Utils/Logger.hpp>
#include <spdlog/spdlog.h> // For spdlog::shutdown()

#include <nlohmann/json.hpp>
#include <string>
#include <vector>
#include <optional>
#include <filesystem>
// #include <iostream> // Can be removed if all output is through logger

using json = nlohmann::json;

int main(int argc, char* argv[]) {
    Launcher::Config launcherConfig;
    std::filesystem::path logDir = launcherConfig.baseDataPath / "logs";
    Launcher::Utils::Logger::Init(logDir, "launcher.log", spdlog::level::trace, spdlog::level::trace);

    CORE_LOG_INFO("Minecraft Launcher v0.1 starting...");
    CORE_LOG_INFO("Data directory: {}", launcherConfig.baseDataPath.string());
    CORE_LOG_INFO("Log directory: {}", logDir.string());

    CORE_LOG_INFO("Fetching version manifest from Mojang...");
    cpr::Response manifest_response = Launcher::Http::Get(cpr::Url{"https://launchermeta.mojang.com/mc/game/version_manifest_v2.json"});
    if (manifest_response.status_code != 200) {
        CORE_LOG_CRITICAL("Failed to fetch version manifest: Status Code: {}, Error: {}",
            manifest_response.status_code, manifest_response.error.message);
        if(!manifest_response.text.empty() && manifest_response.status_code >= 400) {
            CORE_LOG_ERROR("Response Body: {}", manifest_response.text);
        }
        spdlog::shutdown();
        return 1;
    }
    CORE_LOG_INFO("Successfully fetched version manifest (status {}). Size: {} bytes", manifest_response.status_code, manifest_response.text.length());

    json manifest_json;
    try {
        manifest_json = json::parse(manifest_response.text);
        CORE_LOG_TRACE("Version manifest JSON parsed successfully.");
    } catch (const json::parse_error& e) {
        CORE_LOG_CRITICAL("Failed to parse version manifest JSON: {}", e.what());
        spdlog::shutdown();
        return 1;
    }

    std::string version_id_to_parse = "1.20.4";
    CORE_LOG_INFO("Selected Minecraft version for parsing: {}", version_id_to_parse);

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
        CORE_LOG_ERROR("Version {} not found or URL missing in manifest.", version_id_to_parse);
        spdlog::shutdown();
        return 1;
    }
    CORE_LOG_INFO("Found URL for {}: {}", version_id_to_parse, version_url);

    CORE_LOG_INFO("Fetching version details for {} from {}...", version_id_to_parse, version_url);
    cpr::Response version_details_response = Launcher::Http::Get(cpr::Url{version_url});
    if (version_details_response.status_code != 200) {
        CORE_LOG_ERROR("Failed to fetch version details for {}: Status Code: {}, Error: {}",
                  version_id_to_parse, version_details_response.status_code, version_details_response.error.message);
        if(!version_details_response.text.empty() && version_details_response.status_code >= 400) {
            CORE_LOG_ERROR("Response Body: {}", version_details_response.text);
        }
        spdlog::shutdown();
        return 1;
    }
    CORE_LOG_INFO("Successfully fetched version details for {}. Size: {} bytes", version_id_to_parse, version_details_response.text.length());

    json version_data_json;
    try {
        version_data_json = json::parse(version_details_response.text);
        CORE_LOG_TRACE("Version JSON for {} parsed successfully.", version_id_to_parse);
    } catch(const json::parse_error& e) {
        CORE_LOG_CRITICAL("Failed to parse version JSON for {}: {}", version_id_to_parse, e.what());
        spdlog::shutdown();
        return 1;
    }

    Launcher::Version parsed_minecraft_version = Launcher::Version::from_json(version_data_json);
    CORE_LOG_INFO("Successfully parsed Minecraft version object: {}", parsed_minecraft_version.id);
    if (parsed_minecraft_version.javaVersion) {
        CORE_LOG_INFO("  Requires Java Component: {}, Major Version: {}",
                  parsed_minecraft_version.javaVersion->component,
                  parsed_minecraft_version.javaVersion->majorVersion);
    } else {
        CORE_LOG_WARN("  No specific Java version explicitly listed in this version's manifest.");
    }

    Launcher::JavaManager javaManager(launcherConfig); // Logger will be created inside JavaManager
    CORE_LOG_INFO("--- Attempting to ensure Java Runtime ---");
    std::optional<Launcher::JavaRuntime> javaRuntime = javaManager.ensureJavaForMinecraftVersion(parsed_minecraft_version);

    if (javaRuntime) {
        CORE_LOG_INFO("Successfully ensured Java runtime.");
        CORE_LOG_INFO("  Java Home: {}", javaRuntime->homePath.string());
        CORE_LOG_INFO("  Java Executable: {}", javaRuntime->javaExecutablePath.string());
        CORE_LOG_INFO("Next steps would be to use this executable to launch Minecraft.");
    } else {
        CORE_LOG_CRITICAL("Failed to obtain a suitable Java runtime for Minecraft version {}. Exiting.", parsed_minecraft_version.id);
        spdlog::shutdown();
        return 1;
    }

    CORE_LOG_INFO("Minecraft Launcher finished successfully.");
    spdlog::shutdown();
    return 0;
}