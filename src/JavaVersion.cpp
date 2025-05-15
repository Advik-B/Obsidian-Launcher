//
// Created by Advik on 10-05-2025.
//
#include <Launcher/Types/JavaVersion.hpp>

JavaVersion JavaVersion::from_json(const json& j) {
    JavaVersion javaVersion;
    javaVersion.component = j.at("component").get<std::string>();
    javaVersion.majorVersion = j.at("majorVersion").get<unsigned int>();
    return javaVersion;
}