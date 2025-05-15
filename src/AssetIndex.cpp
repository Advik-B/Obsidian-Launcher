//
// Created by Advik on 10-05-2025.
//
#include <Launcher/Types/AssetIndex.hpp>

AssetIndex AssetIndex::from_json(const json& j) {
    AssetIndex assetIndex;
    assetIndex.id = j.at("id").get<std::string>();
    assetIndex.sha1 = j.at("sha1").get<std::string>();
    assetIndex.size = j.at("size").get<size_t>();
    assetIndex.totalSize = j.at("totalSize").get<size_t>();
    assetIndex.url = j.at("url").get<std::string>();
    return assetIndex;
}