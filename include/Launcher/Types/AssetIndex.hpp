//
// Created by Advik on 08-05-2025.
//

#ifndef ASSETINDEX_HPP
#define ASSETINDEX_HPP
#include <string>

struct AssetIndex {
    const std::string id;
    const std::string sha1;
    const size_t size;
    const size_t totalSize;
    const std::string url;
};

#endif //ASSETINDEX_HPP
