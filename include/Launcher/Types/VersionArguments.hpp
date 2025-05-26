// include/Launcher/Types/VersionArguments.hpp
#ifndef VERSIONARGUMENTS_HPP
#define VERSIONARGUMENTS_HPP

#include <string>
#include <vector>
#include <variant>
#include <optional>
#include <map>
#include <Launcher/Types/Rule.hpp> // Includes OS.hpp and nlohmann/json.hpp indirectly

namespace Launcher { // Ensure this namespace is present

    // Forward declarations if not fully included above
    // struct RuleAction; // Rule.hpp should bring this
    // struct OS;         // Rule.hpp -> OS.hpp should bring this
    // using Features = std::map<std::string, bool>; // From Rule.hpp

    struct ArgumentRuleCondition {
        RuleAction action;
        std::optional<OS> os;
        std::optional<Features> features;
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

        // Static member functions declared here
        static Arguments from_json(const json& j);
        static std::vector<VersionArgument> parse_argument_array(const json& arr);
    };

} // namespace Launcher
#endif //VERSIONARGUMENTS_HPP