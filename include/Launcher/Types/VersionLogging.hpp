//
// Created by Advik on 10-05-2025. (Adjusted date for new file)
//

#ifndef VERSIONLOGGING_HPP
#define VERSIONLOGGING_HPP

#include <string>
#include <optional>
#include <nlohmann/json.hpp>

using json = nlohmann::json;

struct LoggingFile {
    std::string id;
    std::string sha1;
    size_t size;
    std::string url;

    static LoggingFile from_json(const json& j);
};

struct ClientLoggingInfo {
    std::string argument;
    LoggingFile file;
    std::string type;

    static ClientLoggingInfo from_json(const json& j);
};

struct LoggingInfo {
    std::optional<ClientLoggingInfo> client;

    static LoggingInfo from_json(const json& j);
};

#endif //VERSIONLOGGING_HPP