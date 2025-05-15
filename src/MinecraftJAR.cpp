//
// Created by Advik on 10-05-2025.
//
#include <Launcher/Types/MinecraftJAR.hpp>
#include <stdexcept>

DownloadDetails DownloadDetails::from_json(const json& j) {
    DownloadDetails dl;
    dl.sha1 = j.at("sha1").get<std::string>();
    dl.size = j.at("size").get<unsigned int>();
    dl.url = j.at("url").get<std::string>();
    return dl;
}

MinecraftJARType string_to_minecraft_jar_type(const std::string& s) {
    if (s == "client") return MinecraftJARType::CLIENT;
    if (s == "server") return MinecraftJARType::SERVER;
    if (s == "client_mappings") return MinecraftJARType::CLIENT_MAPPING;
    if (s == "server_mappings") return MinecraftJARType::SERVER_MAPPING;
    throw std::runtime_error("Unknown Minecraft JAR type string: " + s);
}

std::string minecraft_jar_type_to_string(MinecraftJARType type) {
    switch (type) {
        case MinecraftJARType::CLIENT: return "client";
        case MinecraftJARType::SERVER: return "server";
        case MinecraftJARType::CLIENT_MAPPING: return "client_mappings";
        case MinecraftJARType::SERVER_MAPPING: return "server_mappings";
        default: throw std::runtime_error("Unknown MinecraftJARType enum");
    }
}