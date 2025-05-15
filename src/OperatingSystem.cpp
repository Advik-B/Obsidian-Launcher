//
// Created by Advik on 10-05-2025.
//
#include <Launcher/Types/OS.hpp>

OS OS::from_json(const json& j) {
    OS os_obj;
    if (j.contains("name")) os_obj.name = j.at("name").get<std::string>();
    if (j.contains("version")) os_obj.version = j.at("version").get<std::string>();
    if (j.contains("arch")) os_obj.arch = j.at("arch").get<std::string>();
    return os_obj;
}