//
// Created by Advik on 09-05-2025.
//

#ifndef RULEOS_HPP
#define RULEOS_HPP

#include <string>
#include <optional>
#include <nlohmann/json.hpp>

using json = nlohmann::json;

struct OS {
    std::string name;
    std::optional<std::string> version;
    std::optional<std::string> arch; // For JVM rules

    static OS from_json(const json& j);
};

#endif //RULEOS_HPP