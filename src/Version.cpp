// src/Version.cpp
#include <Launcher/Types/Version.hpp>
#include <Launcher/Utils/Logger.hpp>

namespace Launcher {

Version Version::from_json(const nlohmann::json& j) {
    // Use a temporary logger for this static method or pass one if it becomes complex
    // For now, CORE_LOG is fine for a static utility function like this.
    // Or: static auto s_logger = Utils::Logger::GetOrCreateLogger("VersionParser");
    CORE_LOG_TRACE("[VersionParser] Parsing version JSON for ID: {}", j.value("id", "UNKNOWN_VERSION_ID"));
    Version version;

    if (j.contains("assetIndex")) {
        version.assetIndex = AssetIndex::from_json(j.at("assetIndex"));
    }
    if (j.contains("assets")) {
        version.assets = j.at("assets").get<std::string>();
    }
    if (j.contains("complianceLevel")) {
        version.complianceLevel = j.at("complianceLevel").get<unsigned int>();
    }

    if (j.contains("downloads") && j.at("downloads").is_object()) {
        for (auto& [key, val_json] : j.at("downloads").items()) {
            try {
                MinecraftJARType type = string_to_minecraft_jar_type(key);
                version.downloads[type] = DownloadDetails::from_json(val_json);
            } catch (const std::runtime_error& e) {
                CORE_LOG_WARN("[VersionParser] Skipping unknown download type '{}': {}", key, e.what());
            }
        }
    }

    version.id = j.at("id").get<std::string>();

    if (j.contains("javaVersion")) {
        version.javaVersion = JavaVersion::from_json(j.at("javaVersion"));
    }

    if (j.contains("libraries") && j.at("libraries").is_array()) {
        for (const auto& lib_json : j.at("libraries")) {
            version.libraries.push_back(Library::from_json(lib_json));
        }
    }

    if (j.contains("mainClass")) {
        version.mainClass = j.at("mainClass").get<std::string>();
    }
    if (j.contains("minecraftArguments")) {
        version.minecraftArguments = j.at("minecraftArguments").get<std::string>();
    }
    if (j.contains("minimumLauncherVersion")) {
        version.minimumLauncherVersion = j.at("minimumLauncherVersion").get<unsigned int>();
    }

    version.releaseTime = j.at("releaseTime").get<std::string>();
    version.time = j.at("time").get<std::string>();
    version.type = j.at("type").get<std::string>();

    if (j.contains("arguments")) {
        version.arguments = Arguments::from_json(j.at("arguments"));
    }
    if (j.contains("logging")) {
        version.logging = LoggingInfo::from_json(j.at("logging"));
    }

    CORE_LOG_TRACE("[VersionParser] Successfully parsed version object for ID: {}", version.id);
    return version;
}

} // namespace Launcher