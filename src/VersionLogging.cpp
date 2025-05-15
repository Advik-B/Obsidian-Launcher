//
// Created by Advik on 10-05-2025.
//
#include <Launcher/Types/VersionLogging.hpp>

LoggingFile LoggingFile::from_json(const json& j) {
    LoggingFile file;
    file.id = j.at("id").get<std::string>();
    file.sha1 = j.at("sha1").get<std::string>();
    file.size = j.at("size").get<size_t>();
    file.url = j.at("url").get<std::string>();
    return file;
}

ClientLoggingInfo ClientLoggingInfo::from_json(const json& j) {
    ClientLoggingInfo client_logging;
    client_logging.argument = j.at("argument").get<std::string>();
    client_logging.file = LoggingFile::from_json(j.at("file"));
    client_logging.type = j.at("type").get<std::string>();
    return client_logging;
}

LoggingInfo LoggingInfo::from_json(const json& j) {
    LoggingInfo logging_info;
    if (j.contains("client")) {
        logging_info.client = ClientLoggingInfo::from_json(j.at("client"));
    }
    return logging_info;
}