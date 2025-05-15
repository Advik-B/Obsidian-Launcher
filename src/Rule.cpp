//
// Created by Advik on 10-05-2025.
//
#include <Launcher/Types/Rule.hpp>
#include <stdexcept> // For std::runtime_error

RuleAction string_to_rule_action(const std::string& s) {
    if (s == "allow") return RuleAction::ALLOW;
    if (s == "disallow") return RuleAction::DISALLOW;
    throw std::runtime_error("Unknown rule action: " + s);
}

Rule Rule::from_json(const json& j) {
    Rule rule_obj;
    rule_obj.action = string_to_rule_action(j.at("action").get<std::string>());

    if (j.contains("os")) {
        rule_obj.os = OS::from_json(j.at("os"));
    }

    if (j.contains("features")) {
        Features features_map;
        for (auto& [key, val] : j.at("features").items()) {
            features_map[key] = val.get<bool>();
        }
        rule_obj.features = features_map;
    }
    return rule_obj;
}