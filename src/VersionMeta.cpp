//
// Created by Advik on 08-05-2025.
//

#include <Launcher/Types/VersionMeta.hpp>

#include <date/date.h>
#include <date/tz.h>
#include <chrono>

VersionMeta VersionMeta::from_json(json j) {
    using namespace std::chrono;
    using namespace date;

    const auto id = j["id"].get<std::string>();
    const auto sha1 = j["sha1"].get<std::string>();
    const auto complianceLevel = j["complianceLevel"].get<unsigned int>();

    const auto timeStr = j["time"].get<std::string>();
    const auto releaseTimeStr = j["releaseTime"].get<std::string>();

    // Parse ISO8601 time strings (e.g. 2022-03-10T09:51:38+00:00)
    std::chrono::sys_time<seconds> timePoint;
    std::istringstream in1{timeStr};
    in1 >> date::parse("%FT%T%z", timePoint);
    if (in1.fail()) {
        throw std::runtime_error{"failed to parse time string"};
    }

    std::chrono::sys_time<seconds> releaseTimePoint;
    std::istringstream in2{releaseTimeStr};
    in2 >> date::parse("%FT%T%z", releaseTimePoint);
    if (in2.fail()) {
        throw std::runtime_error{"failed to parse releaseTime string"};
    }

    return VersionMeta{
        id,
        releaseTimePoint,
        timePoint,
        sha1,
        complianceLevel
    };
}
