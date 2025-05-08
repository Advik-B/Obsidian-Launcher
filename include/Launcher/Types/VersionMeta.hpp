//
// Created by Advik on 08-05-2025.
//

#ifndef VERSIONMETA_HPP
#define VERSIONMETA_HPP

#include <string>
#include <chrono>
#include <nlohmann/json.hpp>

using std::string;
using std::chrono::system_clock;

using json = nlohmann::json;

struct VersionMeta {
    const string id;
    const system_clock::time_point releaseTime;
    const system_clock::time_point time;
    const string sha1;
    const unsigned int complianceLevel;

    static VersionMeta from_json(json j);
};

#endif //VERSIONMETA_HPP
