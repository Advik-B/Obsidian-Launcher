//
// Created by Advik on 09-05-2025.
//

#ifndef RULE_HPP
#define RULE_HPP

#include <string>
#include <optional>
#include <map>
#include <Launcher/Types/OS.hpp>
#include <nlohmann/json.hpp>

using std::optional;
using json = nlohmann::json;

enum class RuleAction {
    ALLOW = 1,
    DISALLOW = 2,
};

RuleAction string_to_rule_action(const std::string& s);

typedef std::map<std::string, bool> Features;

struct Rule {
    RuleAction action;
    optional<OS> os;
    optional<Features> features;

    static Rule from_json(const json& j);
};

#endif //RULE_HPP