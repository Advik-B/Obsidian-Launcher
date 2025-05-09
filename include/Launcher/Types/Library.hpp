//
// Created by Advik on 09-05-2025.
//

#ifndef LIBRARY_HPP
#define LIBRARY_HPP

#include <string>
#include <Launcher/Types/Rule.hpp>
#include <unordered_set>

struct LibraryArtifact {
    const std::string path;
    const std::string sha1;
    const unsigned int size;
    const std::string url;
};

struct LibraryDownloads {
    const LibraryArtifact artifact;
};

struct Library {
    const std::string name;
    const LibraryDownloads downloads;
    const std::unordered_set<Rule> rules;
};

#endif //LIBRARY_HPP
