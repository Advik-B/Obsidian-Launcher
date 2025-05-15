//
// Created by Advik on 08-05-2025.
//

#ifndef ASSETINDEX_HPP
#define ASSETINDEX_HPP
#include <string>
#include <nlohmann/json.hpp>

using json = nlohmann::json;

struct AssetIndex {
    std::string id;
    std::string sha1;
    size_t size;
    size_t totalSize;
    std::string url;

    static AssetIndex from_json(const json& j);
};

#endif //ASSETINDEX_HPP