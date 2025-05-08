//
// Created by Advik on 09-05-2025.
//

#ifndef RULE_HPP
#define RULE_HPP

#include <string>
#include <optional>
#include <vector>
#include <Launcher/Types/OS.hpp>

using std::optional;

enum class RuleAction {
    ALLOW = 1,
    DISALLOW = 2,
};

typedef std::vector<std::pair<std::string, bool>> Features;

struct Rule {
    RuleAction action;
    optional<OS> os;
    optional<Features> features;
};

#endif //RULE_HPP
