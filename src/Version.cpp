//
// Created by Advik on 10-05-2025.
//
#include <Launcher/Types/Version.hpp>
#include <iostream> // For std::cerr

Launcher::Version Launcher::Version::from_json(const json& j) {
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
                std::cerr << "Warning: Skipping unknown download type '" << key << "': " << e.what() << std::endl;
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

    return version;
}