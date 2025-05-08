//
// Created by Advik on 08-05-2025.
//

#include <Launcher/Types/VersionMeta.hpp>
#include <chrono>

VersionMeta VersionMeta::from_json(json j) {
    using namespace std::chrono;

    const auto id = j["id"].get<std::string>();
    const auto sha1 = j["sha1"].get<std::string>();
    const auto complianceLevel = j["complianceLevel"].get<unsigned int>();

    const auto timePoint = j["time"].get<std::string>();
    const auto releaseTimePoint = j["releaseTime"].get<std::string>();

    return VersionMeta{
        id,
        releaseTimePoint,
        timePoint,
        sha1,
        complianceLevel
    };
}
