//
// Created by Advik on 08-05-2025.
//

#ifndef VERSION_HPP
#define VERSION_HPP

#include <string>
#include <vector>
#include <map>
#include <optional>
#include <Launcher/Types/AssetIndex.hpp>
#include <Launcher/Types/MinecraftJAR.hpp>
#include <Launcher/Types/JavaVersion.hpp>
#include <Launcher/Types/Library.hpp>
#include <Launcher/Types/VersionArguments.hpp>
#include <Launcher/Types/VersionLogging.hpp>
#include <nlohmann/json.hpp>

namespace Launcher {
    using std::string;
    using json = nlohmann::json;

    struct Version {
        std::optional<AssetIndex> assetIndex;
        string assets;
        std::optional<unsigned int> complianceLevel;
        std::map<MinecraftJARType, DownloadDetails> downloads;
        string id;
        std::optional<JavaVersion> javaVersion;
        std::vector<Library> libraries;
        std::optional<string> mainClass;
        std::optional<string> minecraftArguments; // For old versions
        std::optional<unsigned int> minimumLauncherVersion;
        string releaseTime;
        string time;
        string type; // e.g. "snapshot", "release", "old_alpha"

        // Newer versions
        std::optional<Arguments> arguments;
        std::optional<LoggingInfo> logging;

        static Version from_json(const json& j);
    };
}
#endif //VERSION_HPP