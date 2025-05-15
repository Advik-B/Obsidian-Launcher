//
// Created by Advik on 08-05-2025.
//

#ifndef VERSIONMETA_HPP
#define VERSIONMETA_HPP

#include <string>
#include <chrono>
#include <nlohmann/json.hpp>

using std::string;
using json = nlohmann::json;

struct VersionMeta {
    string id;
    string releaseTime;
    string time;
    string sha1;
    unsigned int complianceLevel;

    static VersionMeta from_json(json j);
};

#endif //VERSIONMETA_HPP