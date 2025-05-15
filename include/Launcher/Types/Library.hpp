//
// Created by Advik on 09-05-2025.
//

#ifndef LIBRARY_HPP
#define LIBRARY_HPP

#include <string>
#include <Launcher/Types/Rule.hpp>
#include <vector>
#include <map>
#include <optional>
#include <nlohmann/json.hpp>

using json = nlohmann::json;

struct LibraryArtifact {
    std::string path;
    std::string sha1;
    unsigned int size;
    std::string url;

    static LibraryArtifact from_json(const json& j);
};

struct LibraryDownloads {
    std::optional<LibraryArtifact> artifact;
    std::map<std::string, LibraryArtifact> classifiers; // Key: e.g., "natives-linux"

    static LibraryDownloads from_json(const json& j);
};

struct LibraryExtractRule {
    std::vector<std::string> exclude;

    static LibraryExtractRule from_json(const json& j);
};

struct Library {
    std::string name;
    std::optional<LibraryDownloads> downloads;
    std::vector<Rule> rules;
    std::map<std::string, std::string> natives; // OS to classifier key e.g. "linux": "natives-linux"
    std::optional<LibraryExtractRule> extract;

    static Library from_json(const json& j);
};

#endif //LIBRARY_HPP