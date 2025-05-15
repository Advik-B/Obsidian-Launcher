//
// Created by Advik on 08-05-2025.
//
#include <Launcher/Types/VersionMeta.hpp>

VersionMeta VersionMeta::from_json(json j) {
    VersionMeta meta;
    meta.id = j.at("id").get<std::string>();
    meta.releaseTime = j.at("releaseTime").get<std::string>();
    meta.time = j.at("time").get<std::string>();
    meta.sha1 = j.at("sha1").get<std::string>();
    meta.complianceLevel = j.at("complianceLevel").get<unsigned int>();
    return meta;
}