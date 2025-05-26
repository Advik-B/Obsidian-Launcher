// src/VersionArguments.cpp
#include <Launcher/Types/VersionArguments.hpp>
#include <Launcher/Utils/Logger.hpp>

namespace Launcher {

// --- ArgumentRuleCondition ---
ArgumentRuleCondition ArgumentRuleCondition::from_json(const json& j) {
    ArgumentRuleCondition arc;
    arc.action = string_to_rule_action(j.at("action").get<std::string>()); // Ensure string_to_rule_action is accessible
    if (j.contains("os")) {
        arc.os = OS::from_json(j.at("os")); // Ensure OS::from_json is accessible
    }
    if (j.contains("features")) {
        Features features_map;
        for (auto& [key, val] : j.at("features").items()) {
            features_map[key] = val.get<bool>();
        }
        arc.features = features_map;
    }
    return arc;
}

// --- ConditionalArgumentValue ---
ConditionalArgumentValue ConditionalArgumentValue::from_json(const json& j) {
    ConditionalArgumentValue cav;
    if (j.contains("rules") && j.at("rules").is_array()) {
        for (const auto& rule_json : j.at("rules")) {
            cav.rules.push_back(ArgumentRuleCondition::from_json(rule_json));
        }
    }
    if (j.contains("value")) {
        if (j.at("value").is_string()) {
            cav.value = j.at("value").get<std::string>();
        } else if (j.at("value").is_array()) {
            std::vector<std::string> values;
            for (const auto& val_item : j.at("value")) {
                values.push_back(val_item.get<std::string>());
            }
            cav.value = values;
        }
    }
    return cav;
}


// --- Arguments ---
// Note: No 'static' keyword here in the definition
std::vector<VersionArgument> Arguments::parse_argument_array(const json& arr) {
    std::vector<VersionArgument> result_args;
    if (arr.is_array()) {
        for (const auto& arg_item_json : arr) {
            if (arg_item_json.is_string()) {
                result_args.emplace_back(arg_item_json.get<std::string>());
            } else if (arg_item_json.is_object()) {
                result_args.emplace_back(ConditionalArgumentValue::from_json(arg_item_json));
            } else {
                 CORE_LOG_WARN("[VersionArgsParser] Unknown argument type in array: {}", arg_item_json.dump(2));
            }
        }
    }
    return result_args;
}

// Note: No 'static' keyword here in the definition
Arguments Arguments::from_json(const json& j) {
    CORE_LOG_TRACE("[VersionArgsParser] Parsing 'arguments' object.");
    Arguments args;
    if (j.contains("game")) {
        args.game = parse_argument_array(j.at("game"));
    }
    if (j.contains("jvm")) {
        args.jvm = parse_argument_array(j.at("jvm"));
    }
    CORE_LOG_TRACE("[VersionArgsParser] Finished parsing 'arguments' object.");
    return args;
}

} // namespace Launcher