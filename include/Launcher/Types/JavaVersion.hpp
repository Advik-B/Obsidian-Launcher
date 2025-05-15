//
// Created by Advik on 09-05-2025.
//

#ifndef JAVAVERSION_HPP
#define JAVAVERSION_HPP

#include <string>
#include <nlohmann/json.hpp>

using json = nlohmann::json;

struct JavaVersion {
    std::string component;
    unsigned int majorVersion;

    static JavaVersion from_json(const json& j);
};

#endif //JAVAVERSION_HPP