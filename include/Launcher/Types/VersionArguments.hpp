//
// Created by Advik on 10-05-2025. (Adjusted date for new file)
//

#ifndef VERSIONARGUMENTS_HPP
#define VERSIONARGUMENTS_HPP

#include <string>
#include <vector>
#include <variant>
#include <optional>
#include <map>
#include <Launcher/Types/Rule.hpp> // Includes OS.hpp and nlohmann/json.hpp indirectly

struct ArgumentRuleCondition { // This is the object inside the "rules" array
    RuleAction action;
    std::optional<OS> os;
    std::optional<Features> features; // Features is std::map<std::string, bool> from Rule.hpp

    static ArgumentRuleCondition from_json(const json& j);
};

struct ConditionalArgumentValue {
    std::vector<ArgumentRuleCondition> rules;
    std::variant<std::string, std::vector<std::string>> value;

    static ConditionalArgumentValue from_json(const json& j);
};

using VersionArgument = std::variant<std::string, ConditionalArgumentValue>;

struct Arguments {
    std::vector<VersionArgument> game;
    std::vector<VersionArgument> jvm;

    static Arguments from_json(const json& j);
    static std::vector<VersionArgument> parse_argument_array(const json& arr);
};

#endif //VERSIONARGUMENTS_HPP