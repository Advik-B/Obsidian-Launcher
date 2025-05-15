//
// Created by Advik on 09-05-2025.
//

#ifndef MINECRAFTJAR_HPP
#define MINECRAFTJAR_HPP
#include <string>
#include <nlohmann/json.hpp>

using json = nlohmann::json;

enum class MinecraftJARType {
    CLIENT = 1,
    SERVER = 2,
    CLIENT_MAPPING = 3,
    SERVER_MAPPING = 4,
};

MinecraftJARType string_to_minecraft_jar_type(const std::string& s);
std::string minecraft_jar_type_to_string(MinecraftJARType type);

struct DownloadDetails {
    std::string sha1;
    unsigned int size;
    std::string url;

    static DownloadDetails from_json(const json& j);
};

#endif //MINECRAFTJAR_HPP