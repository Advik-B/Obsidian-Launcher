//
// Created by Advik on 10-05-2025.
//
#include <Launcher/Types/Library.hpp>
#include <iostream> // For potential debug

LibraryArtifact LibraryArtifact::from_json(const json& j) {
    LibraryArtifact artifact;
    if (j.contains("path")) artifact.path = j.at("path").get<std::string>();
    if (j.contains("sha1")) artifact.sha1 = j.at("sha1").get<std::string>();
    if (j.contains("size")) artifact.size = j.at("size").get<unsigned int>();
    if (j.contains("url")) artifact.url = j.at("url").get<std::string>();
    return artifact;
}

LibraryDownloads LibraryDownloads::from_json(const json& j) {
    LibraryDownloads downloads;
    if (j.contains("artifact")) {
        downloads.artifact = LibraryArtifact::from_json(j.at("artifact"));
    }
    if (j.contains("classifiers")) {
        for (auto& [key, val] : j.at("classifiers").items()) {
            downloads.classifiers[key] = LibraryArtifact::from_json(val);
        }
    }
    return downloads;
}

LibraryExtractRule LibraryExtractRule::from_json(const json& j) {
    LibraryExtractRule extractRule;
    if (j.contains("exclude") && j.at("exclude").is_array()) {
        for (const auto& item : j.at("exclude")) {
            extractRule.exclude.push_back(item.get<std::string>());
        }
    }
    return extractRule;
}

Library Library::from_json(const json& j) {
    Library lib;
    lib.name = j.at("name").get<std::string>();

    if (j.contains("downloads")) {
        lib.downloads = LibraryDownloads::from_json(j.at("downloads"));
    }

    if (j.contains("rules") && j.at("rules").is_array()) {
        for (const auto& rule_json : j.at("rules")) {
            lib.rules.push_back(Rule::from_json(rule_json));
        }
    }

    if (j.contains("natives") && j.at("natives").is_object()) {
        for (auto& [os_key, classifier_val] : j.at("natives").items()) {
            lib.natives[os_key] = classifier_val.get<std::string>();
        }
    }

    if (j.contains("extract") && j.at("extract").is_object()) {
        lib.extract = LibraryExtractRule::from_json(j.at("extract"));
    }

    return lib;
}